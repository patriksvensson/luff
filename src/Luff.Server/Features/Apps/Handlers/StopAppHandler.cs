namespace Luff.Server.Features;

public sealed class StopAppHandler : IRequestHandler<StopAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }
        public string Actor { get; }

        public Request(string name, string actor)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public StopAppHandler(LuffDbContext database, IAgentConnections connections, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<AppResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        app.Stopped = true;

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AppName == app.Name)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            attachment.HealthStatus = AppRuntimeHealth.Stopped;
            attachment.HealthDetail = null;
            _connections.TrySend(attachment.AgentName, new ControlMessage
            {
                StopApp = new StopApp { App = app.Name },
            });
        }

        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AppStopped,
            Actor = request.Actor,
            Title = $"App stopped: {app.Name}",
            Message = $"{app.Name} was manually stopped.",
            App = app.Name,
        }, cancellationToken);

        return app.ToResponse();
    }
}

public static class StopAppHandlerExtensions
{
    public static async Task<AppResponse> StopApp(
        this ISender sender, string name, string actor, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new StopAppHandler.Request(name, actor), cancellationToken);
    }
}
