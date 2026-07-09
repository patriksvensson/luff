namespace Luff.Agent;

public interface IDockerComposeRunner
{
    Task LoginAsync(
        string host,
        string username,
        string password,
        CancellationToken cancellationToken);

    Task<DockerComposeResult> PullAsync(
        string composeYaml,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken);

    Task<DockerComposeResult> UpAsync(
        string composeYaml,
        IReadOnlyDictionary<string, string> environment,
        int? waitTimeoutSeconds,
        CancellationToken cancellationToken);

    Task PruneAsync(
        string app,
        string keepProject,
        CancellationToken cancellationToken);

    Task RemoveAppAsync(
        string app,
        CancellationToken cancellationToken);

    Task StopAppAsync(
        string app,
        CancellationToken cancellationToken);

    Task StartAppAsync(
        string app,
        CancellationToken cancellationToken);

    Task<ContainerStatus?> InspectAsync(
        string app,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContainerReport>> ListManagedAsync(
        CancellationToken cancellationToken);

    Task<string?> TailLogsAsync(
        string app,
        int lines,
        CancellationToken cancellationToken);

    Task StreamLogsAsync(
        string app,
        int tail,
        Action<DockerLogLine> onLine,
        CancellationToken cancellationToken);
}