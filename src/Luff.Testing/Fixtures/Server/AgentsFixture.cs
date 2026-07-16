using Luff.Server.Features;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using AgentEntity = Luff.Server.Features.Agent;

namespace Luff.Server.Tests.Agents;

public sealed class AgentsFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 06, 30, 12, 0, 0, TimeSpan.Zero));

    public FakeAgentConnections Agents { get; } = new();
    public AgentRegistry Registry { get; } = new();
    public FakeLogStream Logs { get; } = new();

    public AgentsFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection, _time);

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider Time => _time;

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }

    public void KnowsAgent(string agentName)
    {
        Registry.MarkConnected(agentName, "1.0.0");
    }

    public void HasConnectedAgent(string agentName)
    {
        Agents.Register(agentName);
    }

    public async Task Attach(AttachAppHandler.Request request)
    {
        var handler = new AttachAppHandler(CreateContext(), Registry, _time);
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task Detach(DetachAppHandler.Request request)
    {
        var handler = new DetachAppHandler(CreateContext(), Agents);
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task<AppStatusHandler.Response> Status(AppStatusHandler.Request request)
    {
        var handler = new AppStatusHandler(CreateContext(), Agents);
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<IAsyncEnumerable<LogEvent>> TailLogs(string app, string? agent)
    {
        var handler = new TailLogsHandler(CreateContext(), Agents, Logs);
        return await handler.Handle(new TailLogsHandler.Request(app, agent), CancellationToken.None);
    }

    public async Task<AppsOverview> Overview()
    {
        var handler = new AppsOverviewHandler(CreateContext(), Agents, Registry, _time);
        return await handler.Handle(new AppsOverviewHandler.Request(), CancellationToken.None);
    }

    public async Task<AppDetail> Detail(string name)
    {
        var handler = new AppDetailHandler(CreateContext(), Agents, Registry, _time);
        return await handler.Handle(new AppDetailHandler.Request(name), CancellationToken.None);
    }

    public async Task<IReadOnlyList<ActivityRow>> Activity()
    {
        var handler = new ActivityHandler(CreateContext(), _time);
        return await handler.Handle(new ActivityHandler.Request(), CancellationToken.None);
    }

    public async Task<EnrollAgentResponse> Enroll(string name)
    {
        var handler = new EnrollAgentHandler(CreateContext(), _time);
        return await handler.Handle(new EnrollAgentHandler.Request(name), CancellationToken.None);
    }

    public async Task RemoveAgent(string name)
    {
        var handler = new RemoveAgentHandler(CreateContext(), Agents, Registry);
        await handler.Handle(new RemoveAgentHandler.Request(name), CancellationToken.None);
    }

    public async Task<IReadOnlyList<FleetAgent>> Fleet()
    {
        var handler = new FleetOverviewHandler(CreateContext(), Registry, _time);
        return await handler.Handle(new FleetOverviewHandler.Request(), CancellationToken.None);
    }

    public async Task HasAgent(string name, DateTimeOffset? lastSeenAt = null)
    {
        await using var context = CreateContext();

        context.Agents.Add(new AgentEntity
        {
            Name = name,
            EnrollmentTokenHash = AgentEntity.Hash($"token-{name}"),
            RegisteredAt = _time.GetUtcNow(),
            LastSeenAt = lastSeenAt,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasDeployment(string appName, string tag, DeploymentStatus status, TimeSpan? age = null)
    {
        await using var context = CreateContext();

        context.Deployments.Add(new Deployment
        {
            Id = Guid.NewGuid(),
            AppName = appName,
            Tag = tag,
            Status = status,
            CreatedAt = _time.GetUtcNow() - (age ?? TimeSpan.Zero),
        });

        await context.SaveChangesAsync();
    }

    public async Task HasApp(
        string name, string? currentImageTag = null, string? previousImageTag = null, string? domain = null,
        TlsMode tlsMode = TlsMode.Managed)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Image = "nginx",
            Domain = domain ?? $"{name}.example.com",
            InternalPort = 80,
            CurrentImageTag = currentImageTag,
            PreviousImageTag = previousImageTag,
            TlsMode = tlsMode,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasAttachment(string appName, string agentName, string? runningTag = null)
    {
        await using var context = CreateContext();

        context.AppAgents.Add(new AppAgent
        {
            AppName = appName,
            AgentName = agentName,
            AttachedAt = _time.GetUtcNow(),
            RunningTag = runningTag,
        });

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AppAgent>> GetAttachments(string appName)
    {
        await using var context = CreateContext();

        return await context.AppAgents
            .Where(attachment => attachment.AppName == appName)
            .ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
