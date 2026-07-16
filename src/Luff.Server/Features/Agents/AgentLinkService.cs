using System.Globalization;

namespace Luff.Server.Features;

public sealed class AgentLinkService : Link.LinkBase
{
    private static readonly string _serverVersion = ServerVersion.Current;

    private static readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(15);

    private readonly IScopedSender _sender;
    private readonly AgentRegistry _registry;
    private readonly IAgentConnections _connections;
    private readonly IDeployEvents _events;
    private readonly IFleetEvents _fleet;
    private readonly ILogStream _logs;
    private readonly ILogger<AgentLinkService> _logger;

    public AgentLinkService(
        IScopedSender sender,
        AgentRegistry registry,
        IAgentConnections connections,
        IDeployEvents events,
        IFleetEvents fleet,
        ILogStream logs,
        ILogger<AgentLinkService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task Connect(
        IAsyncStreamReader<AgentMessage> requestStream,
        IServerStreamWriter<ControlMessage> responseStream,
        ServerCallContext context)
    {
        var name = await Enroll(requestStream, context);

        var outbound = _connections.Register(name);
        _connections.TrySend(name, new ControlMessage
        {
            Welcome = new Welcome
            {
                ServerVersion = _serverVersion,
            },
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var drainLoop = DrainLoop(outbound, responseStream, context.CancellationToken);
        var pingLoop = PingLoop(name, cts.Token);

        await _sender.AgentConnected(name, context.CancellationToken);

        try
        {
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                await Receive(name, requestStream.Current, context.CancellationToken);
            }
        }
        finally
        {
            await cts.CancelAsync();
            _connections.Unregister(name);
            await drainLoop;
            await pingLoop;

            _registry.MarkDisconnected(name);
            _fleet.Publish(name, connected: false);
            _logger.LogInformation("Agent {Agent} disconnected", name);

            await _sender.AgentDisconnected(name, CancellationToken.None);
        }
    }

    private async Task Receive(string name, AgentMessage message, CancellationToken cancellationToken)
    {
        switch (message.PayloadCase)
        {
            case AgentMessage.PayloadOneofCase.Pong:
                _logger.LogDebug("Agent {Agent} pong {Sequence}", name, message.Pong.Sequence);
                break;
            case AgentMessage.PayloadOneofCase.DeployProgress:
                _logger.LogInformation("Agent {Agent} deploy {Deployment}: {Phase}",
                    name, message.DeployProgress.DeploymentId, message.DeployProgress.Phase);
                if (Guid.TryParse(message.DeployProgress.DeploymentId, out var progressId))
                {
                    _events.PublishProgress(progressId, name, message.DeployProgress.Phase);
                }

                break;
            case AgentMessage.PayloadOneofCase.DeployResult:
                if (message.DeployResult.Healthy)
                {
                    _logger.LogInformation("Agent {Agent} deploy {Deployment} succeeded (running {Tag})",
                        name, message.DeployResult.DeploymentId, message.DeployResult.RunningTag);
                }
                else
                {
                    _logger.LogWarning("Agent {Agent} deploy {Deployment} failed: {Reason}",
                        name, message.DeployResult.DeploymentId, message.DeployResult.FailureReason);
                }

                if (Guid.TryParse(message.DeployResult.DeploymentId, out var resultId))
                {
                    _events.PublishResult(
                        resultId,
                        name,
                        message.DeployResult.Healthy,
                        message.DeployResult.RunningTag,
                        message.DeployResult.Healthy ? null : message.DeployResult.FailureReason);
                }

                await HandleDeployResult(name, message.DeployResult, cancellationToken);
                break;
            case AgentMessage.PayloadOneofCase.LogChunk:
                PublishLogChunk(name, message.LogChunk);
                break;
            case AgentMessage.PayloadOneofCase.HealthReport:
                await HandleHealthReport(name, message.HealthReport, cancellationToken);
                break;
        }
    }

    private async Task HandleHealthReport(string agent, HealthReport report, CancellationToken cancellationToken)
    {
        var entries = report.Apps
            .Select(app => new AgentHealthEntry(app.App, MapRuntimeHealth(app.Status), app.Detail))
            .ToList();

        await _sender.ReportAgentHealth(agent, entries, cancellationToken);
    }

    private static AppRuntimeHealth MapRuntimeHealth(RuntimeHealth status) => status switch
    {
        RuntimeHealth.Starting => AppRuntimeHealth.Starting,
        RuntimeHealth.Healthy => AppRuntimeHealth.Healthy,
        RuntimeHealth.Unhealthy => AppRuntimeHealth.Unhealthy,
        RuntimeHealth.Stopped => AppRuntimeHealth.Stopped,
        _ => AppRuntimeHealth.Unknown,
    };

    private void PublishLogChunk(string agent, LogChunk chunk)
    {
        if (!Guid.TryParse(chunk.StreamId, out var streamId))
        {
            return;
        }

        DateTimeOffset? timestamp = DateTimeOffset.TryParse(
            chunk.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

        _logs.PublishChunk(streamId, new LogEvent(timestamp, chunk.Stream, chunk.Line, agent));
    }

    private async Task HandleDeployResult(string agentName, DeployResult result, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(result.DeploymentId, out var deploymentId))
        {
            return;
        }

        await _sender.AgentDeployResult(
            agentName,
            deploymentId,
            healthy: result.Healthy,
            failureReason: result.Healthy ? null : result.FailureReason,
            cancellationToken);
    }

    private async Task<string> Enroll(IAsyncStreamReader<AgentMessage> requestStream, ServerCallContext context)
    {
        if (!await requestStream.MoveNext(context.CancellationToken))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "The agent closed the stream before enrolling"));
        }

        var first = requestStream.Current;
        if (first.PayloadCase != AgentMessage.PayloadOneofCase.Hello)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected a Hello frame to enroll"));
        }

        var hello = first.Hello;
        if (!await _sender.AuthenticateAgent(hello.AgentName, hello.EnrollmentSecret, context.CancellationToken))
        {
            _logger.LogWarning("Rejected agent {Agent}: Invalid enrollment secret", hello.AgentName);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid enrollment secret"));
        }

        _registry.MarkConnected(hello.AgentName, hello.Version, hello.HostsFrontDoor);
        _fleet.Publish(hello.AgentName, connected: true);
        _logger.LogInformation("Agent {Agent} (v{Version}) connected", hello.AgentName, hello.Version);

        return hello.AgentName;
    }

    private static async Task DrainLoop(
        ChannelReader<ControlMessage> outbound,
        IServerStreamWriter<ControlMessage> responseStream,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in outbound.ReadAllAsync(cancellationToken))
            {
                await responseStream.WriteAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PingLoop(string name, CancellationToken cancellationToken)
    {
        var sequence = 0L;
        try
        {
            using var timer = new PeriodicTimer(_pingInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                _connections.TrySend(name, new ControlMessage
                {
                    Ping = new Ping
                    {
                        Sequence = ++sequence,
                    },
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}