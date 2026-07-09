namespace Luff.Agent;

// A single managed container's state, from `docker inspect`. Used to gate a deploy on the container staying
// up (stabilization) and to report ongoing health.
public sealed record ContainerStatus(
    bool Running,
    bool Restarting,
    int RestartCount,
    int? ExitCode,
    string? Health);

// One managed app's runtime state, from a `docker ps` sweep of `luff.managed` containers.
public sealed record ContainerReport(string App, string State, string? Health);
