namespace Luff.Agent.Tests.Fakes;

public sealed class FakeDockerComposeRunner : IDockerComposeRunner
{
    private readonly DockerComposeResult _result;

    public string? Yaml { get; private set; }
    public IReadOnlyDictionary<string, string>? Environment { get; private set; }
    public string? LoginHost { get; private set; }
    public string? LoginUsername { get; private set; }
    public string? LoginPassword { get; private set; }
    public string? PrunedApp { get; private set; }
    public string? KeptProject { get; private set; }
    public string? RemovedApp { get; private set; }
    public int? UpWaitTimeoutSeconds { get; private set; }
    public bool Pulled { get; private set; }
    public string? StreamedApp { get; private set; }
    public int? StreamedTail { get; private set; }

    public DockerComposeResult PullResult { get; set; } = new(true, null);
    public List<DockerLogLine> LogLines { get; set; } = [];

    public FakeDockerComposeRunner(DockerComposeResult result)
    {
        _result = result;
    }

    public Task<DockerComposeResult> PullAsync(
        string composeYaml,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        Pulled = true;
        return Task.FromResult(PullResult);
    }

    public Task LoginAsync(
        string host,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        LoginHost = host;
        LoginUsername = username;
        LoginPassword = password;
        return Task.CompletedTask;
    }

    public Task<DockerComposeResult> UpAsync(
        string composeYaml,
        IReadOnlyDictionary<string, string> environment,
        int? waitTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        Yaml = composeYaml;
        Environment = environment;
        UpWaitTimeoutSeconds = waitTimeoutSeconds;
        return Task.FromResult(_result);
    }

    public Task PruneAsync(
        string app,
        string keepProject,
        CancellationToken cancellationToken)
    {
        PrunedApp = app;
        KeptProject = keepProject;
        return Task.CompletedTask;
    }

    public Task RemoveAppAsync(
        string app,
        CancellationToken cancellationToken)
    {
        RemovedApp = app;
        return Task.CompletedTask;
    }

    public string? StoppedApp { get; private set; }
    public string? StartedApp { get; private set; }

    public DockerComposeResult StopResult { get; set; } = new(true, null);
    public DockerComposeResult StartResult { get; set; } = new(true, null);

    // Inspect returns a healthy, running container by default so a normal deploy passes the stabilization
    // gate; tests set this to a crashed/looping status to exercise the failure path.
    public ContainerStatus? InspectResult { get; set; } = new(true, false, 0, null, DockerHealth.None);
    public List<ContainerReport> ManagedContainers { get; set; } = [];
    public string? TailedLogs { get; set; }

    public Task<DockerComposeResult> StopAppAsync(string app, CancellationToken cancellationToken)
    {
        StoppedApp = app;
        return Task.FromResult(StopResult);
    }

    public Task<DockerComposeResult> StartAppAsync(string app, CancellationToken cancellationToken)
    {
        StartedApp = app;
        return Task.FromResult(StartResult);
    }

    public Task<ContainerStatus?> InspectAsync(string app, CancellationToken cancellationToken)
    {
        return Task.FromResult(InspectResult);
    }

    public Task<IReadOnlyList<ContainerReport>> ListManagedAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ContainerReport>>(ManagedContainers);
    }

    public Task<string?> TailLogsAsync(string app, int lines, CancellationToken cancellationToken)
    {
        return Task.FromResult(TailedLogs);
    }

    public Task StreamLogsAsync(
        string app,
        int tail,
        Action<DockerLogLine> onLine,
        CancellationToken cancellationToken)
    {
        StreamedApp = app;
        StreamedTail = tail;

        foreach (var line in LogLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onLine(line);
        }

        return Task.CompletedTask;
    }
}
