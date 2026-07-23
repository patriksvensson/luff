namespace Luff.Server.Features;

public enum AuditEventKind
{
    DeploySucceeded,
    DeployFailed,
    AppCreated,
    AppUpdated,
    AppDeleted,
    AppStarted,
    AppStopped,
    AppStartFailed,
    AppStopFailed,
    AppUnhealthy,
    AgentConnected,
    AgentDisconnected,
    AgentEnrolled,
    AgentRemoved,
    RegistryAdded,
    RegistryRemoved,
    VolumeAdded,
    VolumeRemoved,
    UserCreated,
    UserDeleted,
    TwoFactorEnabled,
    TwoFactorDisabled,
    ServerStarted,
}
