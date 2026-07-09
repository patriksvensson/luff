namespace Luff.Server.Features;

public sealed class AppOverviewRow
{
    public string Name { get; }
    public bool Internal { get; }
    public bool Direct { get; }
    public string Image { get; }
    public string? Domain { get; }
    public string? InternalHost { get; }
    public bool AutoDomain { get; }
    public bool Https { get; }
    public string? CurrentTag { get; }
    public int MachineCount { get; }
    public AppHealthState State { get; }
    public string? StateDetail { get; }
    public string LastDeploy { get; }

    public AppOverviewRow(
        string name, bool @internal, bool direct, string image, string? domain, string? internalHost,
        bool autoDomain, bool https, string? currentTag, int machineCount, AppHealthState state,
        string? stateDetail, string lastDeploy)
    {
        Name = name;
        Internal = @internal;
        Direct = direct;
        Image = image;
        Domain = domain;
        InternalHost = internalHost;
        AutoDomain = autoDomain;
        Https = https;
        CurrentTag = currentTag;
        MachineCount = machineCount;
        State = state;
        StateDetail = stateDetail;
        LastDeploy = lastDeploy;
    }
}

public sealed class AppsOverview
{
    public IReadOnlyList<AppOverviewRow> Apps { get; }
    public int MachineCount { get; }
    public bool AllConnected { get; }
    public int DeployingCount { get; }

    public AppsOverview(IReadOnlyList<AppOverviewRow> apps, int machineCount, bool allConnected, int deployingCount)
    {
        Apps = apps;
        MachineCount = machineCount;
        AllConnected = allConnected;
        DeployingCount = deployingCount;
    }
}

public sealed class AppsOverviewHandler : IRequestHandler<AppsOverviewHandler.Request, AppsOverview>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly AgentRegistry _registry;
    private readonly TimeProvider _time;

    public sealed class Request : IRequest<AppsOverview>;

    public AppsOverviewHandler(
        LuffDbContext database, IAgentConnections connections, AgentRegistry registry, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task<AppsOverview> Handle(Request request, CancellationToken cancellationToken)
    {
        var apps = await _database.Apps
            .OrderBy(app => app.Name)
            .ToListAsync(cancellationToken);

        var attachments = await _database.AppAgents.ToListAsync(cancellationToken);
        var deployments = await _database.Deployments.ToListAsync(cancellationToken);

        var connected = _connections.Connected.ToHashSet(StringComparer.Ordinal);
        var now = _time.GetUtcNow();

        var rows = apps
            .Select(app => BuildRow(
                app,
                [.. attachments.Where(attachment => attachment.AppName == app.Name).OrderBy(attachment => attachment.AttachedAt)],
                [.. deployments.Where(deployment => deployment.AppName == app.Name).OrderByDescending(deployment => deployment.CreatedAt)],
                now))
            .ToList();

        var known = _registry.List();
        var allConnected = known.Count > 0 && known.All(agent => connected.Contains(agent.Name));
        var deploying = rows.Count(row => row.State == AppHealthState.Deploying);

        return new AppsOverview(rows, known.Count, allConnected, deploying);
    }

    private static AppOverviewRow BuildRow(
        App app, IReadOnlyList<AppAgent> attachments, IReadOnlyList<Deployment> deployments, DateTimeOffset now)
    {
        var latest = deployments.Count > 0 ? deployments[0] : null;
        var inFlight = deployments.Any(deployment =>
            deployment.Status is DeploymentStatus.Pending or DeploymentStatus.InProgress);

        var (state, detail) = AppHealth.Classify(app, attachments, latest, inFlight);

        var isInternal = app.Kind == AppKind.Internal;
        var isDirect = app.Kind == AppKind.Direct;
        var autoDomain = AppHealth.IsAutoDomain(app.Domain);
        var internalHost = isInternal ? $"{app.Name}:{app.InternalPort}" : null;
        return new AppOverviewRow(
            app.Name, isInternal, isDirect, app.Image, app.Domain, internalHost, autoDomain,
            https: !isInternal && !isDirect && !autoDomain,
            app.CurrentImageTag, attachments.Count, state, detail, AppHealth.LastDeployText(latest, now));
    }
}
