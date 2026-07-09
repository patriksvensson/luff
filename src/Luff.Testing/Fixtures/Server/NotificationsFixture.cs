using Luff.Server.Features;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Server.Tests.Notifications;

public sealed class NotificationsFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly TimeProvider _time;

    public FakeSecretProtector Protector { get; } = new();
    public FakeNotificationDispatcher Dispatcher { get; } = new();

    public NotificationsFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LuffDbContext>()
            .UseSqlite(_connection)
            .Options;

        _time = new FakeTimeProvider(new DateTimeOffset(2026, 07, 09, 12, 0, 0, TimeSpan.Zero));

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }

    public async Task<NotificationChannelResponse> AddChannel(string name, string type, string url)
    {
        var handler = new AddNotificationChannelHandler(CreateContext(), Protector, _time);
        return await handler.Handle(
            new AddNotificationChannelHandler.Request(name, type, url), CancellationToken.None);
    }

    public async Task<IReadOnlyList<NotificationChannelResponse>> ListChannels()
    {
        var handler = new ListNotificationChannelsHandler(CreateContext());
        return await handler.Handle(new ListNotificationChannelsHandler.Request(), CancellationToken.None);
    }

    public async Task RemoveChannel(Guid id)
    {
        var handler = new RemoveNotificationChannelHandler(CreateContext());
        await handler.Handle(new RemoveNotificationChannelHandler.Request(id), CancellationToken.None);
    }

    public async Task TestChannel(Guid id)
    {
        var handler = new TestNotificationChannelHandler(CreateContext(), Dispatcher, Protector);
        await handler.Handle(new TestNotificationChannelHandler.Request(id), CancellationToken.None);
    }

    public async Task Publish(Alert alert)
    {
        var publisher = new AlertPublisher(
            CreateContext(), Dispatcher, Protector, NullLogger<AlertPublisher>.Instance);
        await publisher.PublishAsync(alert);
    }

    public async Task DisableChannel(Guid id)
    {
        await using var context = CreateContext();
        var channel = await context.NotificationChannels.FindAsync(id);
        channel!.Enabled = false;
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
