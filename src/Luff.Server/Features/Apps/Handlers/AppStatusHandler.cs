namespace Luff.Server.Features;

public sealed class AppStatusHandler : IRequestHandler<AppStatusHandler.Request, AppStatusHandler.Response>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;

    public sealed class Request : IRequest<Response>
    {
        public string AppName { get; }

        public Request(string appName)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        }
    }

    public sealed class Response
    {
        public string? CurrentImageTag { get; }
        public IReadOnlyList<AgentStatus> Agents { get; }

        public sealed class AgentStatus
        {
            public string Agent { get; }
            public string? RunningTag { get; }
            public bool Connected { get; }

            public AgentStatus(string agent, string? runningTag, bool connected)
            {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
                RunningTag = runningTag;
                Connected = connected;
            }
        }

        public Response(string? currentImageTag, IReadOnlyList<AgentStatus> agents)
        {
            CurrentImageTag = currentImageTag;
            Agents = agents ?? throw new ArgumentNullException(nameof(agents));
        }
    }

    public AppStatusHandler(LuffDbContext database, IAgentConnections connections)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken)
            ?? throw new AppNotFoundException(request.AppName);

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AppName == request.AppName)
            .ToListAsync(cancellationToken);

        var connected = _connections.Connected;
        var agents = attachments
            .OrderBy(attachment => attachment.AttachedAt)
            .Select(attachment =>
                new Response.AgentStatus(attachment.AgentName, attachment.RunningTag, connected.Contains(attachment.AgentName)))
            .ToList();

        return new Response(app.CurrentImageTag, agents);
    }
}

public static class AppStatusHandlerExtensions
{
    public static async Task<AppStatusHandler.Response> AppStatus(
        this ISender sender, string appName, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new AppStatusHandler.Request(appName), cancellationToken);
    }
}
