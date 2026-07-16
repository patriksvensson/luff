using Luff.Server.Features;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Luff.Server.Tests.Apps;

public sealed class AppsFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;

    public FakeAgentConnections Agents { get; }

    public AppsFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection);

        Agents = new FakeAgentConnections();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public async Task<AppResponse> CreateApp(
        string name, string image, string domain, int internalPort, TlsMode? tlsMode = null)
    {
        var handler = new CreateAppHandler(CreateContext());
        return await handler.Handle(
            new CreateAppHandler.Request(name, image, internalPort, domain: domain, tlsMode: tlsMode),
            CancellationToken.None);
    }

    public async Task<AppResponse> CreateApp(CreateAppHandler.Request request)
    {
        var handler = new CreateAppHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task DeleteApp(string name)
    {
        var handler = new DeleteAppHandler(CreateContext());
        await handler.Handle(
            new DeleteAppHandler.Request(name),
            CancellationToken.None);
    }

    public async Task<AppResponse> GetApp(string name)
    {
        var handler = new GetAppHandler(CreateContext());
        return await handler.Handle(
            new GetAppHandler.Request(name),
            CancellationToken.None);
    }

    public async Task<IReadOnlyList<AppResponse>> ListApps()
    {
        var handler = new ListAppsHandler(CreateContext());
        return await handler.Handle(
            new ListAppsHandler.Request(),
            CancellationToken.None);
    }

    public async Task<AppResponse> SetHealthCheck(string name, AppHealthCheckType type, string? endpoint, int timeoutSeconds)
    {
        var handler = new SetHealthCheckHandler(CreateContext());
        return await handler.Handle(
            new SetHealthCheckHandler.Request(name, type, endpoint, timeoutSeconds),
            CancellationToken.None);
    }

    public async Task<AppResponse> UpdateApp(
        string name, string image, string domain, int internalPort, TlsMode? tlsMode = null)
    {
        var handler = new UpdateAppHandler(CreateContext(), Agents);
        return await handler.Handle(
            new UpdateAppHandler.Request(name, image, internalPort, domain: domain, tlsMode: tlsMode),
            CancellationToken.None);
    }

    public async Task<AppResponse> UpdateApp(UpdateAppHandler.Request request)
    {
        var handler = new UpdateAppHandler(CreateContext(), Agents);
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<AppResponse> StopApp(string name)
    {
        var handler = new StopAppHandler(CreateContext(), Agents, Alerts);
        return await handler.Handle(new StopAppHandler.Request(name), CancellationToken.None);
    }

    public async Task<AppResponse> StartApp(string name)
    {
        var handler = new StartAppHandler(CreateContext(), Agents, Alerts);
        return await handler.Handle(new StartAppHandler.Request(name), CancellationToken.None);
    }

    public async Task<AppAgent?> GetAttachment(string appName, string agentName)
    {
        await using var context = CreateContext();
        return await context.AppAgents.FindAsync(appName, agentName);
    }

    public FakeAlertPublisher Alerts { get; } = new();

    public async Task ReportHealth(string agentName, IReadOnlyList<AgentHealthEntry> entries)
    {
        var handler = new ReportAgentHealthHandler(CreateContext(), Alerts, TimeProvider.System);
        await handler.Handle(new ReportAgentHealthHandler.Request(agentName, entries), CancellationToken.None);
    }

    public async Task HasApp(
        string name = "web", string image = "nginx", string? domain = "web.example.com", int internalPort = 80,
        TlsMode tlsMode = TlsMode.Managed, AppKind kind = AppKind.Web)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Kind = kind,
            Image = image,
            Domain = domain,
            InternalPort = internalPort,
            TlsMode = tlsMode,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasInternalApp(string name = "postgres", string image = "postgres", int internalPort = 5432)
    {
        await HasApp(name, image, domain: null, internalPort, kind: AppKind.Internal);
    }

    public async Task HasAttachment(string appName, string agentName)
    {
        await using var context = CreateContext();

        context.AppAgents.Add(new AppAgent
        {
            AppName = appName,
            AgentName = agentName,
            AttachedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        });

        await context.SaveChangesAsync();
    }

    public async Task<App?> GetAppFromDatabase(string name)
    {
        await using var context = CreateContext();
        return await context.Apps.FindAsync(name);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }
}
