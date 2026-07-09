namespace Luff.Server.Features;

public sealed class DetachAppHandler : IRequestHandler<DetachAppHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;

    public sealed class Request : IRequest<Unit>
    {
        public string AgentName { get; }
        public string AppName { get; }

        public Request(string agentName, string appName)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        }
    }

    public DetachAppHandler(LuffDbContext database, IAgentConnections connections)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var attachment = await _database.AppAgents.FindAsync(
                             [request.AppName, request.AgentName], cancellationToken)
                         ?? throw new AttachmentNotFoundException(request.AgentName, request.AppName);

        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken);

        _database.AppAgents.Remove(attachment);
        await _database.SaveChangesAsync(cancellationToken);

        if (app is not null)
        {
            _connections.TrySend(request.AgentName, new ControlMessage
            {
                Remove = new Remove
                {
                    App = app.Name,
                    // Empty for an internal service: the agent then only tears down the container, no route.
                    Domain = app.Domain ?? string.Empty,
                },
            });
        }

        return Unit.Value;
    }
}

public static class DetachAppHandlerExtensions
{
    public static async Task DetachApp(
        this ISender sender, string agentName, string appName, CancellationToken cancellationToken = default)
    {
        await sender.Send(new DetachAppHandler.Request(agentName, appName), cancellationToken);
    }
}