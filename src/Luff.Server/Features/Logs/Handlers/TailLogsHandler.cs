namespace Luff.Server.Features;

public sealed class TailLogsHandler : IRequestHandler<TailLogsHandler.Request, IAsyncEnumerable<LogEvent>>
{
    private const int TailLines = 200;

    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly ILogStream _logStream;

    public sealed class Request : IRequest<IAsyncEnumerable<LogEvent>>
    {
        public string AppName { get; }
        public string? Agent { get; }

        public Request(string appName, string? agent)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Agent = agent;
        }
    }

    public TailLogsHandler(LuffDbContext database, IAgentConnections connections, ILogStream logStream)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _logStream = logStream ?? throw new ArgumentNullException(nameof(logStream));
    }

    public async Task<IAsyncEnumerable<LogEvent>> Handle(Request request, CancellationToken cancellationToken)
    {
        _ = await _database.Apps.FindAsync([request.AppName], cancellationToken)
            ?? throw new AppNotFoundException(request.AppName);

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AppName == request.AppName)
            .ToListAsync(cancellationToken);

        var agent = ResolveAgent(request, attachments);

        if (!_connections.Connected.Contains(agent))
        {
            throw new AgentNotConnectedException(agent);
        }

        return _logStream.Tail(agent, request.AppName, TailLines, cancellationToken);
    }

    private static string ResolveAgent(Request request, IReadOnlyList<AppAgent> attachments)
    {
        if (request.Agent is not null)
        {
            if (!attachments.Any(attachment => attachment.AgentName == request.Agent))
            {
                throw new AttachmentNotFoundException(request.Agent, request.AppName);
            }

            return request.Agent;
        }

        if (attachments.Count != 1)
        {
            throw new AgentSelectionRequiredException(request.AppName, attachments.Count);
        }

        return attachments[0].AgentName;
    }
}

public static class TailLogsHandlerExtensions
{
    public static async Task<IAsyncEnumerable<LogEvent>> TailLogs(
        this ISender sender, string appName, string? agent, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new TailLogsHandler.Request(appName, agent), cancellationToken);
    }
}
