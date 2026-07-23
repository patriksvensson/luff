namespace Luff.Server.Features;

public sealed class AppMachineLine
{
    public string Agent { get; }
    public string? RunningTag { get; }
    public bool Connected { get; }
    public bool Behind { get; }
    public bool FrontDoor { get; }
    public string Health { get; }

    public AppMachineLine(
        string agent, string? runningTag, bool connected, bool behind, bool frontDoor, string health)
    {
        Agent = agent;
        RunningTag = runningTag;
        Connected = connected;
        Behind = behind;
        FrontDoor = frontDoor;
        Health = health;
    }
}

public sealed class AppDeploymentLine
{
    public string Tag { get; }
    public string Status { get; }
    public bool Succeeded { get; }
    public bool Failed { get; }
    public string? FailureReason { get; }
    public string When { get; }

    public AppDeploymentLine(
        string tag, string status, bool succeeded, bool failed, string? failureReason, string when)
    {
        Tag = tag;
        Status = status;
        Succeeded = succeeded;
        Failed = failed;
        FailureReason = failureReason;
        When = when;
    }
}

public sealed class AppDetail
{
    public string Name { get; }
    public AppKind Kind { get; }
    public bool Internal { get; }
    public bool Direct { get; }
    public bool Stopped { get; }
    public string Image { get; }
    public string? Domain { get; }
    public string? InternalHost { get; }
    public bool AutoDomain { get; }
    public int InternalPort { get; }
    public string? CurrentTag { get; }
    public string? PreviousTag { get; }
    public string TlsLabel { get; }
    public bool TlsTrusted { get; }
    public string TlsMode { get; }
    public bool Https { get; }
    public AppHealthState State { get; }
    public string? StateDetail { get; }
    public int BehindCount { get; }
    public IReadOnlyList<AppMachineLine> Machines { get; }
    public IReadOnlyList<AppDeploymentLine> Deployments { get; }

    public AppDetail(
        string name, AppKind kind, bool @internal, bool direct, bool stopped, string image, string? domain,
        string? internalHost, bool autoDomain, int internalPort, string? currentTag, string? previousTag,
        string tlsLabel, bool tlsTrusted, string tlsMode, bool https, AppHealthState state, string? stateDetail,
        int behindCount, IReadOnlyList<AppMachineLine> machines, IReadOnlyList<AppDeploymentLine> deployments)
    {
        Name = name;
        Kind = kind;
        Internal = @internal;
        Direct = direct;
        Stopped = stopped;
        Image = image;
        Domain = domain;
        InternalHost = internalHost;
        AutoDomain = autoDomain;
        InternalPort = internalPort;
        CurrentTag = currentTag;
        PreviousTag = previousTag;
        TlsLabel = tlsLabel;
        TlsTrusted = tlsTrusted;
        TlsMode = tlsMode;
        Https = https;
        State = state;
        StateDetail = stateDetail;
        BehindCount = behindCount;
        Machines = machines;
        Deployments = deployments;
    }
}

public sealed class AppDetailHandler : IRequestHandler<AppDetailHandler.Request, AppDetail>
{
    private const int RecentDeployments = 20;

    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly AgentRegistry _registry;
    private readonly TimeProvider _time;

    public sealed class Request : IRequest<AppDetail>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public AppDetailHandler(
        LuffDbContext database, IAgentConnections connections, AgentRegistry registry, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task<AppDetail> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AppName == request.Name)
            .ToListAsync(cancellationToken);

        var deployments = await _database.Deployments
            .Where(deployment => deployment.AppName == request.Name)
            .ToListAsync(cancellationToken);

        var ordered = attachments.OrderBy(attachment => attachment.AttachedAt).ToList();
        var history = deployments.OrderByDescending(deployment => deployment.CreatedAt).ToList();

        var connected = _connections.Connected.ToHashSet(StringComparer.Ordinal);
        var now = _time.GetUtcNow();
        var latest = history.Count > 0 ? history[0] : null;
        var inFlight = history.Any(deployment =>
            deployment.Status is DeploymentStatus.Pending or DeploymentStatus.InProgress);

        var (state, detail) = AppHealth.Classify(app, ordered, latest, inFlight);

        var machines = ordered
            .Select(attachment => new AppMachineLine(
                attachment.AgentName,
                attachment.RunningTag,
                connected.Contains(attachment.AgentName),
                app.CurrentImageTag is not null && attachment.RunningTag != app.CurrentImageTag,
                _registry.IsFrontDoorHost(attachment.AgentName),
                connected.Contains(attachment.AgentName)
                    ? attachment.HealthStatus.ToString().ToLowerInvariant()
                    : "unknown"))
            .ToList();

        var lines = history
            .Take(RecentDeployments)
            .Select(deployment => new AppDeploymentLine(
                deployment.Tag,
                StatusLabel(deployment.Status),
                deployment.Status == DeploymentStatus.Succeeded,
                deployment.Status == DeploymentStatus.Failed,
                deployment.FailureReason,
                AppHealth.Relative(now - deployment.CreatedAt)))
            .ToList();

        var isInternal = app.Kind == AppKind.Internal;
        var isDirect = app.Kind == AppKind.Direct;
        var autoDomain = AppHealth.IsAutoDomain(app.Domain);

        // Public-URL scheme: a real domain is reached over HTTPS whether Caddy (managed) or a load balancer
        // (external) terminates it. sslip.io defaults are plain HTTP. Internal and direct apps are not exposed.
        var https = !isInternal && !isDirect && !autoDomain;
        var (tlsLabel, tlsTrusted) = isInternal
            ? ("Internal", false)
            : isDirect
                ? ("Direct", false)
                : app.TlsMode == TlsMode.External
                    ? ("External", false)
                    : autoDomain
                        ? ("Plain HTTP", false)
                        : ("Publicly trusted", true);

        var internalHost = isInternal ? $"{app.Name}:{app.InternalPort}" : null;

        return new AppDetail(
            app.Name, app.Kind, isInternal, isDirect, app.Stopped, app.Image, app.Domain, internalHost, autoDomain,
            app.InternalPort, app.CurrentImageTag, app.PreviousImageTag,
            tlsLabel, tlsTrusted, app.TlsMode.ToString(), https,
            state, detail, machines.Count(machine => machine.Behind), machines, lines);
    }

    private static string StatusLabel(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Succeeded => "Succeeded",
        DeploymentStatus.Failed => "Failed",
        DeploymentStatus.InProgress => "In progress",
        _ => "pending",
    };
}
