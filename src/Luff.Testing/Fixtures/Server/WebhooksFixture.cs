using Luff.Server.Features;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Server.Tests.Webhooks;

public sealed class WebhooksFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly TimeProvider _time;

    public FakeAgentConnections Agents { get; } = new();

    public WebhooksFixture()
    {
        Agents.Register("agent-1");

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection, _time);

        _time = new FakeTimeProvider(new DateTimeOffset(2026, 06, 29, 19, 55, 0, TimeSpan.Zero));

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }

    public async Task<CreateTokenResponse> CreateToken(CreateWebhookTokenHandler.Request request)
    {
        var handler = new CreateWebhookTokenHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task RevokeToken(RevokeWebhookTokenHandler.Request request)
    {
        var handler = new RevokeWebhookTokenHandler(CreateContext());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task<IReadOnlyList<TokenResponse>> ListTokens(ListWebhookTokensHandler.Request request)
    {
        var handler = new ListWebhookTokensHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<DeploymentResponse> TriggerWebhook(TriggerWebhookHandler.Request request)
    {
        await using var context = CreateContext();
        var engine = new DeployEngine(
            context, Agents, new FakeSecretProtector(), new FakeEventPublisher());
        var handler = new TriggerWebhookHandler(context, _time, engine);
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task HasAttachment(string appName, string agentName)
    {
        await using var context = CreateContext();

        context.AppAgents.Add(new AppAgent
        {
            AppName = appName,
            AgentName = agentName,
            AttachedAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    public async Task HasApp(string name, bool stopped = false)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Image = "nginx",
            Domain = $"{name}.example.com",
            InternalPort = 80,
            Stopped = stopped,
        });

        await context.SaveChangesAsync();
    }

    public async Task<Guid> HasToken(string appName, string token, string name = "ci")
    {
        await using var context = CreateContext();

        var entity = new WebhookToken
        {
            Id = Guid.NewGuid(),
            AppName = appName,
            Name = name,
            TokenHash = WebhookToken.Hash(token),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.WebhookTokens.Add(entity);
        await context.SaveChangesAsync();

        return entity.Id;
    }

    public async Task<IReadOnlyList<WebhookToken>> GetTokens(string appName)
    {
        await using var context = CreateContext();

        return await context.WebhookTokens
            .Where(token => token.AppName == appName)
            .ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}