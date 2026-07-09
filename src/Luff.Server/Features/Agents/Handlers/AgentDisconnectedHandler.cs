namespace Luff.Server.Features;

public sealed class AgentDisconnectedHandler : IRequestHandler<AgentDisconnectedHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly DeployEngine _engine;
    private readonly IAlertPublisher _alerts;

    public sealed class Request : IRequest<Unit>
    {
        public string AgentName { get; }

        public Request(string agentName)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        }
    }

    public AgentDisconnectedHandler(LuffDbContext database, DeployEngine engine, IAlertPublisher alerts)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        await _alerts.PublishAsync(new Alert(
            AlertKind.AgentDisconnected,
            $"Agent disconnected: {request.AgentName}",
            $"Agent '{request.AgentName}' lost its link to the control plane.",
            null,
            request.AgentName), cancellationToken);

        // A disconnected agent can no longer vouch for its containers; its reported health is now stale.
        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AgentName == request.AgentName
                && attachment.HealthStatus != AppRuntimeHealth.Unknown)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            attachment.HealthStatus = AppRuntimeHealth.Unknown;
            attachment.HealthDetail = null;
        }

        if (attachments.Count > 0)
        {
            await _database.SaveChangesAsync(cancellationToken);
        }

        await _engine.HandleAgentDisconnectedAsync(request.AgentName, cancellationToken);
        return Unit.Value;
    }
}

public static class AgentDisconnectedHandlerExtensions
{
    public static async Task AgentDisconnected(
        this IScopedSender sender, string agentName, CancellationToken cancellationToken = default)
    {
        await sender.Send(new AgentDisconnectedHandler.Request(agentName), cancellationToken);
    }
}
