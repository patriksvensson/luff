namespace Luff.Server.Features;

public sealed class DeployRequest
{
    public string? Tag { get; init; }
}

public sealed class DeploymentResponse
{
    public Guid Id { get; }
    public string App { get; }
    public string Tag { get; }
    public string Status { get; }
    public string? FailureReason { get; }
    public DateTimeOffset CreatedAt { get; }

    public DeploymentResponse(
        Guid id, string app, string tag, string status, string? failureReason, DateTimeOffset createdAt)
    {
        Id = id;
        App = app ?? throw new ArgumentNullException(nameof(app));
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        FailureReason = failureReason;
        CreatedAt = createdAt;
    }
}
