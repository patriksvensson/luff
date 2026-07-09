namespace Luff.Server.Features;

public sealed class ActivityRow
{
    public string App { get; }
    public string Tag { get; }
    public string Status { get; }
    public bool Succeeded { get; }
    public bool Failed { get; }
    public string? FailureReason { get; }
    public int MachineCount { get; }
    public string When { get; }

    public ActivityRow(
        string app, string tag, string status, bool succeeded, bool failed,
        string? failureReason, int machineCount, string when)
    {
        App = app;
        Tag = tag;
        Status = status;
        Succeeded = succeeded;
        Failed = failed;
        FailureReason = failureReason;
        MachineCount = machineCount;
        When = when;
    }
}

public sealed class ActivityHandler : IRequestHandler<ActivityHandler.Request, IReadOnlyList<ActivityRow>>
{
    private const int Recent = 50;

    private readonly LuffDbContext _database;
    private readonly TimeProvider _time;

    public sealed class Request : IRequest<IReadOnlyList<ActivityRow>>;

    public ActivityHandler(LuffDbContext database, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task<IReadOnlyList<ActivityRow>> Handle(Request request, CancellationToken cancellationToken)
    {
        var deployments = await _database.Deployments.ToListAsync(cancellationToken);
        var now = _time.GetUtcNow();

        return
        [
            .. deployments
                .OrderByDescending(deployment => deployment.CreatedAt)
                .Take(Recent)
                .Select(deployment => new ActivityRow(
                    deployment.AppName,
                    deployment.Tag,
                    StatusLabel(deployment.Status),
                    deployment.Status == DeploymentStatus.Succeeded,
                    deployment.Status == DeploymentStatus.Failed,
                    deployment.FailureReason,
                    deployment.Agents.Count,
                    AppHealth.Relative(now - deployment.CreatedAt))),
        ];
    }

    private static string StatusLabel(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Succeeded => "succeeded",
        DeploymentStatus.Failed => "failed",
        DeploymentStatus.InProgress => "in progress",
        _ => "pending",
    };
}
