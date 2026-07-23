namespace Luff.Server.Features;

public sealed class StartAppHandler : IRequestHandler<StartAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;

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

    public StartAppHandler(LuffDbContext database, IAgentConnections connections)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
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
            // The agent confirms the real outcome over the link; show it as coming up meanwhile.
            attachment.HealthStatus = AppRuntimeHealth.Starting;
            attachment.HealthDetail = null;
            _connections.TrySend(attachment.AgentName, new ControlMessage
            {
                StartApp = new StartApp { App = app.Name, Actor = request.Actor },
            });
        }

        await _database.SaveChangesAsync(cancellationToken);

        return app.ToResponse();
    }
}

public static class StartAppHandlerExtensions
{
    public static async Task<AppResponse> StartApp(
        this ISender sender, string name, string actor, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new StartAppHandler.Request(name, actor), cancellationToken);
    }
}
