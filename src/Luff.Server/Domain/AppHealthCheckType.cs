namespace Luff.Server.Features;

public enum AppHealthCheckType
{
    Docker = 0,
    Http = 1,
    None = 2,

    // Agent-side TCP connect to the container's port over the shared network. Needs no in-container tooling,
    // so it suits internal services like databases.
    Tcp = 3,
}