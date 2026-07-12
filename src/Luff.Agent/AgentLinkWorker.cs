using System.Collections.Concurrent;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Grpc.Core;
using Grpc.Net.Client;
using Luff.Protobuf;
using Microsoft.Extensions.Options;

namespace Luff.Agent;

public sealed class AgentLinkWorker : BackgroundService
{
    private static readonly string _agentVersion =
        typeof(AgentLinkWorker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.0.0";

    private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _healthInterval = TimeSpan.FromSeconds(10);

    private readonly AgentOptions _options;
    private readonly AgentDeployRunner _agentDeployRunner;
    private readonly IDockerComposeRunner _dockerCompose;
    private readonly AgentLogStreamer _logStreamer;
    private readonly ILogger<AgentLinkWorker> _logger;

    public AgentLinkWorker(
        IOptions<AgentOptions> options,
        AgentDeployRunner agentDeployRunner,
        IDockerComposeRunner dockerCompose,
        AgentLogStreamer logStreamer,
        ILogger<AgentLinkWorker> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _agentDeployRunner = agentDeployRunner ?? throw new ArgumentNullException(nameof(agentDeployRunner));
        _dockerCompose = dockerCompose ?? throw new ArgumentNullException(nameof(dockerCompose));
        _logStreamer = logStreamer ?? throw new ArgumentNullException(nameof(logStreamer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndServe(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Link to {Address} dropped. Reconnecting in {Delay} seconds",
                    _options.ServerAddress, _reconnectDelay.TotalSeconds);
            }

            try
            {
                await Task.Delay(_reconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndServe(CancellationToken stoppingToken)
    {
        using var channel = CreateChannel();
        var client = new Link.LinkClient(channel);

        using var call = client.Connect(cancellationToken: stoppingToken);

        // Write Hello message
        await call.RequestStream.WriteAsync(new AgentMessage
        {
            Hello = new Hello
            {
                AgentName = _options.Name,
                EnrollmentSecret = _options.EnrollmentSecret,
                Version = _agentVersion,
                HostsFrontDoor = _options.HostsFrontDoor,
            },
        }, stoppingToken);

        _logger.LogInformation("Sent Hello as {Agent} to {Address}", _options.Name, _options.ServerAddress);

        // Fire-and-forget log tails make this a concurrent writer, so every outbound frame after Hello is
        // funnelled through a single channel drained by one writer (mirrors the control plane's DrainLoop)
        var outbound = Channel.CreateUnbounded<AgentMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        var logStreams = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var drainTask = DrainOutbound(call.RequestStream, outbound.Reader, connectionCts.Token);
        var healthTask = HealthReportLoop(outbound.Writer, connectionCts.Token);

        try
        {
            await foreach (var message in call.ResponseStream.ReadAllAsync(stoppingToken))
            {
                switch (message.PayloadCase)
                {
                    case ControlMessage.PayloadOneofCase.Welcome:
                        _logger.LogInformation("Enrolled with control plane v{Version}",
                            message.Welcome.ServerVersion);
                        break;
                    case ControlMessage.PayloadOneofCase.Ping:
                        outbound.Writer.TryWrite(new AgentMessage
                        {
                            Pong = new Pong
                            {
                                Sequence = message.Ping.Sequence,
                            },
                        });
                        break;
                    case ControlMessage.PayloadOneofCase.Deploy:
                        await HandleDeploy(outbound.Writer, message.Deploy, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.Remove:
                        await HandleRemove(message.Remove, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.Reroute:
                        await HandleReroute(message.Reroute, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.ConfigureFrontDoor:
                        await HandleConfigureFrontDoor(message.ConfigureFrontDoor, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.AssertRoute:
                        await HandleAssertRoute(message.AssertRoute, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.StartLogStream:
                        StartLogStream(outbound.Writer, logStreams, message.StartLogStream, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.StopLogStream:
                        StopLogStream(logStreams, message.StopLogStream);
                        break;
                    case ControlMessage.PayloadOneofCase.StopApp:
                        await HandleStopApp(message.StopApp, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.StartApp:
                        await HandleStartApp(message.StartApp, stoppingToken);
                        break;
                    case ControlMessage.PayloadOneofCase.None:
                        break;
                    default:
                        _logger.LogInformation("Received unknown payload: {Payload}", message.PayloadCase);
                        break;
                }
            }
        }
        finally
        {
            foreach (var stream in logStreams.Values)
            {
                stream.Cancel();
                stream.Dispose();
            }

            logStreams.Clear();

            outbound.Writer.TryComplete();
            await connectionCts.CancelAsync();
            await drainTask;
            await healthTask;
        }
    }

    private GrpcChannel CreateChannel()
    {
        if (!string.IsNullOrEmpty(_options.ServerPin))
        {
            var pin = _options.ServerPin;
            var handler = new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                        certificate is X509Certificate2 server && ServerCertificatePin.Matches(server, pin),
                },
            };

            return GrpcChannel.ForAddress(_options.ServerAddress, new GrpcChannelOptions
            {
                HttpHandler = handler,
                DisposeHttpClient = true,
            });
        }

        return GrpcChannel.ForAddress(_options.ServerAddress);
    }

    private async Task HealthReportLoop(ChannelWriter<AgentMessage> outbound, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_healthInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                IReadOnlyList<ContainerReport> reports;
                try
                {
                    reports = await _dockerCompose.ListManagedAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health poll failed; will retry");
                    continue;
                }

                var report = new HealthReport();
                foreach (var container in reports)
                {
                    report.Apps.Add(new ContainerHealth
                    {
                        App = container.App,
                        Status = MapHealth(container.State, container.Health),
                        Detail = container.Health ?? container.State,
                    });
                }

                outbound.TryWrite(new AgentMessage { HealthReport = report });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static RuntimeHealth MapHealth(string state, string? health)
    {
        if (string.Equals(health, "unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeHealth.Unhealthy;
        }

        if (string.Equals(health, "starting", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeHealth.Starting;
        }

        return state.ToLowerInvariant() switch
        {
            "running" => RuntimeHealth.Healthy,
            "restarting" => RuntimeHealth.Unhealthy,
            "exited" => RuntimeHealth.Stopped,
            _ => RuntimeHealth.Unknown,
        };
    }

    private async Task HandleStopApp(StopApp stop, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {App}", stop.App);
        await _agentDeployRunner.StopAppAsync(stop.App, cancellationToken);
    }

    private async Task HandleStartApp(StartApp start, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {App}", start.App);
        await _agentDeployRunner.StartAppAsync(start.App, cancellationToken);
    }

    private static async Task DrainOutbound(
        IClientStreamWriter<AgentMessage> requestStream,
        ChannelReader<AgentMessage> outbound,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in outbound.ReadAllAsync(cancellationToken))
            {
                await requestStream.WriteAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
            // The gRPC call is already torn down. The receive loop drives reconnection
        }
    }

    private async Task HandleDeploy(
        ChannelWriter<AgentMessage> outbound, Deploy deploy, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deploying {App}@{Tag} (deployment {Deployment})",
            deploy.App, deploy.Tag, deploy.DeploymentId);

        var result = await _agentDeployRunner.RunAsync(
            deploy,
            (phase, _) =>
            {
                outbound.TryWrite(new AgentMessage
                {
                    DeployProgress = new DeployProgress
                    {
                        DeploymentId = deploy.DeploymentId,
                        Phase = phase,
                    },
                });
                return Task.CompletedTask;
            },
            cancellationToken);

        outbound.TryWrite(new AgentMessage
        {
            DeployResult = result,
        });
    }

    private void StartLogStream(
        ChannelWriter<AgentMessage> outbound,
        ConcurrentDictionary<string, CancellationTokenSource> logStreams,
        StartLogStream request,
        CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        if (!logStreams.TryAdd(request.StreamId, cts))
        {
            cts.Dispose();
            return;
        }

        _logger.LogInformation("Streaming logs for {App} (stream {Stream})", request.App, request.StreamId);
        _ = RunLogStream(outbound, logStreams, request, cts);
    }

    private async Task RunLogStream(
        ChannelWriter<AgentMessage> outbound,
        ConcurrentDictionary<string, CancellationTokenSource> logStreams,
        StartLogStream request,
        CancellationTokenSource cts)
    {
        try
        {
            await _logStreamer.StreamAsync(
                request.StreamId,
                request.App,
                request.Tail,
                message => outbound.TryWrite(message),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Log stream {Stream} for {App} ended with an error",
                request.StreamId, request.App);
        }
        finally
        {
            if (logStreams.TryRemove(request.StreamId, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    private static void StopLogStream(
        ConcurrentDictionary<string, CancellationTokenSource> logStreams, StopLogStream request)
    {
        if (logStreams.TryRemove(request.StreamId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task HandleRemove(Remove remove, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing {App} ({Domain})", remove.App, remove.Domain);
        await _agentDeployRunner.RemoveAsync(remove.App, remove.Domain, cancellationToken);
    }

    private async Task HandleReroute(Reroute reroute, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rerouting {App} from {OldDomain} to {NewDomain}",
            reroute.App, reroute.OldDomain, reroute.NewDomain);
        await _agentDeployRunner.RerouteAsync(
            reroute.OldDomain, reroute.NewDomain, reroute.Route, cancellationToken);
    }

    private async Task HandleAssertRoute(AssertRoute assert, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Re-asserting route for {App} ({Domain})", assert.App, assert.Domain);

        // Re-assert runs on a freshly-reconnected link and may cover several apps. 
        // One route failure must not tear the link down (the reconnect loop would just retry into the same failure)
        try
        {
            await _agentDeployRunner.AssertRouteAsync(
                assert.Domain, assert.Upstream, assert.Route, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-assert the route for {App}; the link stays up", assert.App);
        }
    }

    private async Task HandleConfigureFrontDoor(ConfigureFrontDoor frontDoor, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring the front door for {Domain} -> {Upstream}",
            frontDoor.Domain, frontDoor.Upstream);

        try
        {
            // A front-door failure must not tear down the whole link (deploys, logs, routes). Log it and carry on.
            // The control plane re-pushes on the next reconnect
            await _agentDeployRunner.ConfigureFrontDoorAsync(frontDoor.Domain, frontDoor.Upstream, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure the front door for {Domain}; the link stays up",
                frontDoor.Domain);
        }
    }
}
