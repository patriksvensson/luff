using Luff.Agent.Tests.Fakes;
using Luff.Protobuf;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Agent.Tests.Fixtures;

public sealed class DeployRunnerFixture
{
    public FakeDockerComposeRunner DockerCompose { get; }
    public FakeCaddyClient Caddy { get; }
    public FakeTcpProbe TcpProbe { get; }
    public FakeTimeProvider Clock { get; }
    public List<string> Phases { get; } = [];

    private DeployRunnerFixture(bool succeed, string? output)
    {
        DockerCompose = new FakeDockerComposeRunner(new DockerComposeResult(succeed, output));
        Caddy = new FakeCaddyClient();
        TcpProbe = new FakeTcpProbe();
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero));
    }

    public async Task<DeployResult> RunAsync(Deploy deploy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploy.Compose))
        {
            // Set the compose to something valid
            deploy.Compose = $"name: {deploy.App ?? "luff-web-d1"}";
        }

        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        return await runner.RunAsync(
            deploy,
            (phase, _) =>
            {
                Phases.Add(phase);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public async Task RemoveAsync(string app, string domain, CancellationToken cancellationToken = default)
    {
        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        await runner.RemoveAsync(app, domain, cancellationToken);
    }

    public async Task StopAppAsync(string app, CancellationToken cancellationToken = default)
    {
        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        await runner.StopAppAsync(app, cancellationToken);
    }

    public async Task StartAppAsync(string app, CancellationToken cancellationToken = default)
    {
        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        await runner.StartAppAsync(app, cancellationToken);
    }

    public async Task RerouteAsync(
        string oldDomain, string newDomain, TlsRoute route = TlsRoute.Http,
        CancellationToken cancellationToken = default)
    {
        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        await runner.RerouteAsync(oldDomain, newDomain, route, cancellationToken);
    }

    public async Task AssertRouteAsync(
        string domain, string upstream, TlsRoute route = TlsRoute.Managed,
        CancellationToken cancellationToken = default)
    {
        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        await runner.AssertRouteAsync(domain, upstream, route, cancellationToken);
    }

    public async Task ConfigureFrontDoorAsync(string domain, string upstream, CancellationToken cancellationToken = default)
    {
        var runner = new AgentDeployRunner(DockerCompose, Caddy, TcpProbe, Clock);
        await runner.ConfigureFrontDoorAsync(domain, upstream, cancellationToken);
    }

    public static DeployRunnerFixture CreateForSuccess()
    {
        return new DeployRunnerFixture(true, null);
    }

    public static DeployRunnerFixture CreateForFailure(string output)
    {
        return new DeployRunnerFixture(false, output);
    }
}