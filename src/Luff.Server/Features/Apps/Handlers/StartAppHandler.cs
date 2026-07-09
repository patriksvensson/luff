namespace Luff.Server.Features;

public sealed class StartAppHandler : IRequestHandler<StartAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly IAlertPublisher _alerts;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public StartAppHandler(LuffDbContext database, IAgentConnections connections, IAlertPublisher alerts)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
    }

    public async Task<AppResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        app.Stopped = false;

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AppName == app.Name)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            // The agent's next health report settles the real status; show it as coming up meanwhile.
            attachment.HealthStatus = AppRuntimeHealth.Starting;
            attachment.HealthDetail = null;
            _connections.TrySend(attachment.AgentName, new ControlMessage
            {
                StartApp = new StartApp { App = app.Name },
            });
        }

        await _database.SaveChangesAsync(cancellationToken);

        await _alerts.PublishAsync(new Alert(
            AlertKind.AppStarted,
            $"App started: {app.Name}",
            $"{app.Name} was manually started.",
            app.Name), cancellationToken);

        return app.ToResponse();
    }
}

public static class StartAppHandlerExtensions
{
    public static async Task<AppResponse> StartApp(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new StartAppHandler.Request(name), cancellationToken);
    }
}
