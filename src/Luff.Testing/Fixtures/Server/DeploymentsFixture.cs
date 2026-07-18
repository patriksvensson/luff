using Luff.Server.Features;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Server.Tests.Deployments;

public sealed class DeploymentsFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly FakeTimeProvider _time;
    private int _attachOrder;

    public FakeAgentConnections Agents { get; }
    public FakeEventPublisher Events { get; } = new();
    public DeployEngine DeployEngine { get; }

    public DeploymentsFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection, _time);

        _time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

        using var context = CreateContext();
        context.Database.EnsureCreated();

        Agents = new FakeAgentConnections();
        Agents.Register("agent-1");
        DeployEngine = CreateEngine(CreateContext());
    }

    public DeployEngine CreateEngine(LuffDbContext context)
    {
        return new DeployEngine(
            context,
            Agents,
            new FakeSecretProtector(),
            Events);
    }

    public async Task<DeploymentResponse> TriggerDeployment(string name, string? tag, string actor = "operator@example.com")
    {
        await using var context = CreateContext();
        var handler = new TriggerDeploymentHandler(context, CreateEngine(context));
        return await handler.Handle(new TriggerDeploymentHandler.Request(name, tag, actor), CancellationToken.None);
    }

    public async Task<DeploymentResponse> Rollback(string name, string actor = "operator@example.com")
    {
        await using var context = CreateContext();
        var handler = new RollbackHandler(context, CreateEngine(context));
        return await handler.Handle(new RollbackHandler.Request(name, actor), CancellationToken.None);
    }

    public async Task<IReadOnlyList<DeploymentResponse>> ListDeployments(string name)
    {
        var handler = new ListDeploymentsHandler(CreateContext());
        return await handler.Handle(new ListDeploymentsHandler.Request(name), CancellationToken.None);
    }

    public async Task HasApp(string name, string? currentImageTag = null, string image = "nginx",
        AppHealthCheckType appHealthCheckType = AppHealthCheckType.Docker, string? healthCheckEndpoint = null,
        string? previousImageTag = null, string? domain = null, TlsMode tlsMode = TlsMode.Managed,
        AppKind kind = AppKind.Web, int internalPort = 80, bool stopped = false)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Kind = kind,
            Image = image,
            Domain = kind == AppKind.Internal ? null : domain ?? $"{name}.example.com",
            InternalPort = internalPort,
            Stopped = stopped,
            CurrentImageTag = currentImageTag,
            PreviousImageTag = previousImageTag,
            HealthCheckType = appHealthCheckType,
            HealthCheckEndpoint = healthCheckEndpoint,
            TlsMode = tlsMode,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasPendingDeployment(string appName, string tag)
    {
        await using var context = CreateContext();

        context.Deployments.Add(new Deployment
        {
            Id = Guid.NewGuid(),
            AppName = appName,
            Tag = tag,
            Status = DeploymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasInProgressDeployment(string appName, string tag, params string[] agents)
    {
        await using var context = CreateContext();

        context.Deployments.Add(new Deployment
        {
            Id = Guid.NewGuid(),
            AppName = appName,
            Tag = tag,
            Status = DeploymentStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            Agents = [.. agents],
        });

        await context.SaveChangesAsync();
    }

    public async Task HasEnvVar(string appName, string key, string protectedValue)
    {
        await using var context = CreateContext();

        context.EnvVars.Add(new EnvVar
        {
            AppName = appName,
            Key = key,
            Value = protectedValue,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasRegistry(string host, string username, string protectedPassword)
    {
        await using var context = CreateContext();

        context.Registries.Add(new Registry
        {
            Host = host,
            Username = username,
            Password = protectedPassword,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasVolume(string appName, string source, string target, bool readOnly)
    {
        await using var context = CreateContext();

        context.Volumes.Add(new Volume
        {
            AppName = appName,
            Source = source,
            Target = target,
            ReadOnly = readOnly,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasAttachment(
        string appName, string agentName, string? runningTag = null, Guid? runningDeploymentId = null)
    {
        await using var context = CreateContext();

        context.AppAgents.Add(new AppAgent
        {
            AppName = appName,
            AgentName = agentName,
            AttachedAt = new DateTimeOffset(2026, 06, 30, 0, 0, 0, TimeSpan.Zero).AddSeconds(_attachOrder++),
            RunningTag = runningTag,
            RunningDeploymentId = runningDeploymentId,
        });

        await context.SaveChangesAsync();
    }

    public async Task<AppAgent?> FindAttachment(string appName, string agentName)
    {
        await using var context = CreateContext();
        return await context.AppAgents.FindAsync(appName, agentName);
    }

    public async Task<App?> FindApp(string name)
    {
        await using var context = CreateContext();
        return await context.Apps.FindAsync(name);
    }

    public async Task<IReadOnlyList<Deployment>> GetDeployments(string appName)
    {
        await using var context = CreateContext();

        return await context.Deployments
            .Where(deployment => deployment.AppName == appName)
            .ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }
}