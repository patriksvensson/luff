namespace Luff.Server.Features;

public sealed class Deployment
{
    public required Guid Id { get; init; }
    public required string AppName { get; init; }
    public required string Tag { get; set; }
    public required DeploymentStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public List<string> Agents { get; set; } = [];
    public int AgentCursor { get; set; }

    public DeploymentResponse ToResponse()
    {
        return new DeploymentResponse(
            Id, AppName, Tag,
            Status,
            FailureReason, CreatedAt);
    }
}

public enum DeploymentStatus
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    Failed = 3,
}
