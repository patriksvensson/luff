namespace Luff.Server.Features;

public sealed class AttachAppHandler : IRequestHandler<AttachAppHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly AgentRegistry _registry;
    private readonly TimeProvider _timeProvider;

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

    public AttachAppHandler(LuffDbContext database, AgentRegistry registry, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken)
            ?? throw new AppNotFoundException(request.AppName);

        if (!_registry.Knows(request.AgentName))
        {
            throw new AgentNotFoundException(request.AgentName);
        }

        var existing = await _database.AppAgents.FindAsync([app.Name, request.AgentName], cancellationToken);
        if (existing is null)
        {
            _database.AppAgents.Add(new AppAgent
            {
                AppName = app.Name,
                AgentName = request.AgentName,
                AttachedAt = _timeProvider.GetUtcNow(),
            });

            await _database.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}

public static class AttachAppHandlerExtensions
{
    public static async Task AttachApp(
        this ISender sender, string agentName,
        string appName, CancellationToken cancellationToken = default)
    {
        await sender.Send(new AttachAppHandler.Request(agentName, appName), cancellationToken);
    }
}
