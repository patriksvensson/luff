namespace Luff.Server.Components.Shared;

public sealed class DeployLane
{
    public required string Agent { get; init; }
    public DeployPhase? Phase { get; set; }
    public bool Started { get; set; }
    public bool Done { get; set; }
    public bool Failed { get; set; }
    public string? OldTag { get; set; }
    public string? ServingTag { get; set; }
}
