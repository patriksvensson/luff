namespace Luff.Server.Features;

public sealed class Deployment : Entity
{
    public required Guid Id { get; init; }
    public required string AppName { get; init; }
    public required string Tag { get; set; }
    public required DeploymentStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public List<string> Agents { get; set; } = [];
    public int AgentCursor { get; set; }

    // Who set this rollout in motion: an operator's email, "ci" for a webhook, or "system" for an automatic
    // catch-up. Stamped on the deploy-succeeded / deploy-failed audit events so the log answers "who shipped".
    public string TriggeredBy { get; set; } = Actors.System;

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
