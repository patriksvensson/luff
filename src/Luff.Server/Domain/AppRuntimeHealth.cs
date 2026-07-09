namespace Luff.Server.Features;

// Live per-agent container health, reported by the agent while connected. Distinct from AppHealthState,
// which is the app-wide status the UI shows (folding these in across all attached agents).
public enum AppRuntimeHealth
{
    Unknown = 0,
    Starting = 1,
    Healthy = 2,
    Unhealthy = 3,
    Stopped = 4,
}
