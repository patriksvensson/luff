using Luff.Protobuf;

namespace Luff.Agent;

public delegate Task DeployProgressReporter(DeployPhase phase, CancellationToken cancellationToken);

public sealed class AgentDeployRunner
{
    private static readonly TimeSpan _probeInterval = TimeSpan.FromSeconds(2);

    private readonly IDockerComposeRunner _dockerCompose;
    private readonly ICaddyClient _caddy;
    private readonly ITcpProbe _tcpProbe;
    private readonly TimeProvider _timeProvider;

    public AgentDeployRunner(
        IDockerComposeRunner dockerCompose, ICaddyClient caddy, ITcpProbe tcpProbe, TimeProvider timeProvider)
    {
        _dockerCompose = dockerCompose ?? throw new ArgumentNullException(nameof(dockerCompose));
        _caddy = caddy ?? throw new ArgumentNullException(nameof(caddy));
        _tcpProbe = tcpProbe ?? throw new ArgumentNullException(nameof(tcpProbe));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DeployResult> RunAsync(
        Deploy deploy, DeployProgressReporter? report, CancellationToken cancellationToken)
    {
        Task Report(DeployPhase phase) => report is null ? Task.CompletedTask : report(phase, cancellationToken);

        var violation = DockerComposeValidator.Validate(deploy.Compose);
        if (violation is not null)
        {
            return Failure(deploy, $"Compose validation failed: {violation}");
        }

        if (deploy.Registry is not null)
        {
            try
            {
                await _dockerCompose.LoginAsync(
                    deploy.Registry.Host,
                    deploy.Registry.Username,
                    deploy.Registry.Password,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Failure(deploy, $"Registry login failed: {exception.Message}");
            }
        }

        var timeout = deploy.HealthTimeoutSeconds > 0 ? deploy.HealthTimeoutSeconds : 300;

        await Report(DeployPhase.Pulling);
        var pull = await _dockerCompose.PullAsync(deploy.Compose, deploy.Env, cancellationToken);
        if (!pull.Succeeded)
        {
            return Failure(deploy, pull.Output ?? "Compose pull failed");
        }

        await Report(DeployPhase.Starting);

        // Docker/HTTP health gates on the container's own healthcheck via `--wait`. TCP and None run their own
        // readiness poll below (which fails fast on a crash), so they start detached.
        var composeWaits = deploy.HealthKind is HealthCheckKind.Docker or HealthCheckKind.Http;
        var compose = await _dockerCompose.UpAsync(
            deploy.Compose,
            deploy.Env,
            waitTimeoutSeconds: composeWaits ? timeout : null,
            cancellationToken);

        if (!compose.Succeeded)
        {
            return Failure(deploy, compose.Output ?? "Compose up failed");
        }

        // Health gate. Whatever the kind, a crash-looping container fails fast with its logs rather than
        // spinning until the timeout. `docker compose up --wait` only proves "running", and a restart policy
        // masks a crash loop, which is what let a broken deploy report healthy.
        var reason = composeWaits
            ? await StabilizeReasonAsync(deploy.App, cancellationToken)
            : await WaitForReadyAsync(deploy, timeout, cancellationToken);

        if (reason is not null)
        {
            return Failure(deploy, await Describe(deploy, reason, cancellationToken));
        }

        await Report(DeployPhase.Healthy);

        // An internal service carries no domain: it is recreated in place (stable compose project) and has no
        // Caddy route, so there is nothing to swap.
        if (!string.IsNullOrEmpty(deploy.Domain))
        {
            await Report(DeployPhase.Swapping);
            try
            {
                await _caddy.ConfigureRouteAsync(
                    deploy.Domain, deploy.Upstream, deploy.TlsRoute,
                    BasicAuth.From(deploy.BasicAuthUsername, deploy.BasicAuthHash), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Failure(deploy, $"Route configuration failed: {exception.Message}");
            }
        }

        await Report(DeployPhase.Draining);
        await _dockerCompose.PruneAsync(
            deploy.App,
            keepProject: deploy.Project,
            cancellationToken);

        return new DeployResult
        {
            DeploymentId = deploy.DeploymentId,
            Healthy = true,
            RunningTag = deploy.Tag,
        };
    }

    public async Task RemoveAsync(string app, string domain, CancellationToken cancellationToken)
    {
        await _dockerCompose.RemoveAppAsync(app, cancellationToken);

        // Internal services have no route; only tear down the container.
        if (!string.IsNullOrEmpty(domain))
        {
            await _caddy.RemoveRouteAsync(domain, cancellationToken);
        }
    }

    public Task StopAppAsync(string app, CancellationToken cancellationToken) =>
        _dockerCompose.StopAppAsync(app, cancellationToken);

    public Task StartAppAsync(string app, CancellationToken cancellationToken) =>
        _dockerCompose.StartAppAsync(app, cancellationToken);

    // Poll until the container is ready, failing fast the moment it crashes instead of waiting out the timeout.
    // Used for TCP and None, which start detached (no `docker compose --wait`).
    private async Task<string?> WaitForReadyAsync(Deploy deploy, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var (host, port) = ProbeTarget(deploy);
        var deadline = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(timeoutSeconds);

        while (true)
        {
            var status = await _dockerCompose.InspectAsync(deploy.App, cancellationToken);

            var crash = CrashReason(status);
            if (crash is not null)
            {
                return crash;
            }

            var running = status?.Running ?? false;
            var ready = deploy.HealthKind == HealthCheckKind.Tcp
                ? running && await _tcpProbe.TryConnectAsync(host, port, cancellationToken)
                : running;

            if (ready)
            {
                return null;
            }

            if (_timeProvider.GetUtcNow() + _probeInterval >= deadline)
            {
                return deploy.HealthKind == HealthCheckKind.Tcp
                    ? $"did not accept a TCP connection within {timeoutSeconds}s"
                    : $"did not start within {timeoutSeconds}s";
            }

            await Task.Delay(_probeInterval, _timeProvider, cancellationToken);
        }
    }

    private static (string Host, int Port) ProbeTarget(Deploy deploy)
    {
        if (!string.IsNullOrEmpty(deploy.Upstream))
        {
            var separator = deploy.Upstream.LastIndexOf(':');
            if (separator > 0 && int.TryParse(deploy.Upstream[(separator + 1)..], out var upstreamPort))
            {
                return (deploy.Upstream[..separator], upstreamPort);
            }
        }

        return (deploy.App, deploy.InternalPort);
    }

    // Strict post-`--wait` check: by now the container should be running, so anything else is a failure.
    private async Task<string?> StabilizeReasonAsync(string app, CancellationToken cancellationToken)
    {
        var status = await _dockerCompose.InspectAsync(app, cancellationToken);
        if (status is null)
        {
            return null;
        }

        if (status.Restarting || status.RestartCount > 0)
        {
            return $"the container is restart-looping (restarted {status.RestartCount} times)";
        }

        if (!status.Running)
        {
            return status.ExitCode is { } code
                ? $"the container exited with code {code}"
                : "the container is not running";
        }

        if (status.Health == DockerHealth.Unhealthy)
        {
            return "the container reported an unhealthy health check";
        }

        return null;
    }

    // Lenient crash check for the readiness poll: a container that is still coming up (not yet running, but
    // hasn't errored) is not a crash, so we keep waiting. Only an actual restart loop or a non-zero exit fails.
    private static string? CrashReason(ContainerStatus? status)
    {
        if (status is null)
        {
            return null;
        }

        if (status.RestartCount > 0)
        {
            return $"the container is restart-looping (restarted {status.RestartCount} times)";
        }

        if (!status.Running && status.ExitCode is { } code && code != 0)
        {
            return $"the container exited with code {code}";
        }

        return null;
    }

    private async Task<string> Describe(Deploy deploy, string reason, CancellationToken cancellationToken)
    {
        var logs = await _dockerCompose.TailLogsAsync(deploy.App, 20, cancellationToken);
        return string.IsNullOrEmpty(logs)
            ? $"{deploy.App} {reason}"
            : $"{deploy.App} {reason}:\n{logs}";
    }

    public async Task RerouteAsync(
        string oldDomain, string newDomain, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken)
    {
        await _caddy.RerouteAsync(oldDomain, newDomain, route, basicAuth, cancellationToken);
    }

    public async Task AssertRouteAsync(
        string domain, string upstream, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken)
    {
        // Make Caddy match the control plane's route truth on reconnect. Drop any stale route (possibly on the
        // wrong server, or gone after a Caddy restart) and recreate it fresh on the correct server
        await _caddy.RemoveRouteAsync(domain, cancellationToken);
        await _caddy.ConfigureRouteAsync(domain, upstream, route, basicAuth, cancellationToken);
    }

    public async Task ConfigureFrontDoorAsync(
        string domain, string upstream, bool managedTls, CancellationToken cancellationToken)
    {
        await _caddy.ConfigureFrontDoorAsync(domain, upstream, managedTls, cancellationToken);
    }

    private static DeployResult Failure(Deploy deploy, string reason)
    {
        return new DeployResult
        {
            DeploymentId = deploy.DeploymentId,
            Healthy = false,
            FailureReason = reason,
        };
    }
}