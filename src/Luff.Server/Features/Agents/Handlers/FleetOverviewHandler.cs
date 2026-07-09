namespace Luff.Server.Features;

public sealed class FleetAgent
{
    public string Name { get; }
    public string Status { get; }
    public string? Version { get; }
    public bool FrontDoor { get; }
    public IReadOnlyList<string> Apps { get; }
    public string? LastSeen { get; }

    public FleetAgent(
        string name, string status, string? version, bool frontDoor, IReadOnlyList<string> apps, string? lastSeen)
    {
        Name = name;
        Status = status;
        Version = version;
        FrontDoor = frontDoor;
        Apps = apps;
        LastSeen = lastSeen;
    }
}

public sealed class FleetOverviewHandler : IRequestHandler<FleetOverviewHandler.Request, IReadOnlyList<FleetAgent>>
{
    private readonly LuffDbContext _database;
    private readonly AgentRegistry _registry;
    private readonly TimeProvider _time;

    public sealed class Request : IRequest<IReadOnlyList<FleetAgent>>;

    public FleetOverviewHandler(LuffDbContext database, AgentRegistry registry, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task<IReadOnlyList<FleetAgent>> Handle(Request request, CancellationToken cancellationToken)
    {
        var agents = await _database.Agents.ToListAsync(cancellationToken);
        var attachments = await _database.AppAgents.ToListAsync(cancellationToken);
        var snapshots = _registry.List().ToDictionary(snapshot => snapshot.Name, StringComparer.Ordinal);
        var now = _time.GetUtcNow();

        return
        [
            .. agents
                .OrderBy(agent => agent.RegisteredAt)
                .Select(agent =>
                {
                    snapshots.TryGetValue(agent.Name, out var snapshot);

                    var status = snapshot is not null
                        ? snapshot.Status == AgentConnectionStatus.Connected ? "connected" : "disconnected"
                        : agent.LastSeenAt is null ? "pending" : "disconnected";

                    var apps = attachments
                        .Where(attachment => attachment.AgentName == agent.Name)
                        .Select(attachment => attachment.AppName)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToList();

                    var lastSeen = status != "connected" && agent.LastSeenAt is not null
                        ? AppHealth.Relative(now - agent.LastSeenAt.Value)
                        : null;

                    return new FleetAgent(
                        agent.Name, status, snapshot?.Version, snapshot?.HostsFrontDoor ?? false, apps, lastSeen);
                }),
        ];
    }
}
