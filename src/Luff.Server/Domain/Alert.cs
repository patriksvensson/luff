namespace Luff.Server.Features;

public enum AlertKind
{
    DeployFailed,
    DeploySucceeded,
    AgentConnected,
    AgentDisconnected,
    AppUnhealthy,
    AppStarted,
    AppStopped,
}

public sealed record Alert(AlertKind Kind, string Title, string Message, string? App = null, string? Agent = null);
