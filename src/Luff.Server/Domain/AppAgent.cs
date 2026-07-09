namespace Luff.Server.Features;

public sealed class AppAgent
{
    public required string AppName { get; init; }
    public required string AgentName { get; init; }
    public required DateTimeOffset AttachedAt { get; init; }
    public string? RunningTag { get; set; }
    public Guid? RunningDeploymentId { get; set; }
    public AppRuntimeHealth HealthStatus { get; set; } = AppRuntimeHealth.Unknown;
    public string? HealthDetail { get; set; }
    public DateTimeOffset? HealthReportedAt { get; set; }
}
