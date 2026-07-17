using Luff.Server.Features;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Server.Tests.Audit;

public sealed class AuditFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 07, 17, 12, 0, 0, TimeSpan.Zero));

    public AuditFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection, _time);

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider Time => _time;

    public LuffDbContext CreateContext() => new(_options);

    public AuditLogListener CreateAuditLogListener() => new(new FakeServiceScopeFactory(CreateContext));

    public async Task<IReadOnlyList<AuditEventResponse>> ListAudit()
    {
        var handler = new ListAuditHandler(CreateContext());
        return await handler.Handle(new ListAuditHandler.Request(), CancellationToken.None);
    }

    public async Task HasAuditEvent(
        AuditEventKind kind, string title, string actor = Actors.System, string message = "",
        string? app = null, string? agent = null, TimeSpan? age = null)
    {
        await using var context = CreateContext();

        context.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            Actor = actor,
            Title = title,
            Message = message,
            App = app,
            Agent = agent,
            CreatedAt = _time.GetUtcNow() - (age ?? TimeSpan.Zero),
        });

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AuditEvent>> AllEvents()
    {
        await using var context = CreateContext();
        return await context.AuditEvents.ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
