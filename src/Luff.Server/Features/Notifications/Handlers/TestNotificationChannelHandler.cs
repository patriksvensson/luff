namespace Luff.Server.Features;

public sealed class TestNotificationChannelHandler : IRequestHandler<TestNotificationChannelHandler.Request, Unit>
{
    private static readonly AuditEvent TestEvent = new()
    {
        Kind = AuditEventKind.DeployFailed,
        Actor = Actors.System,
        Title = "Test notification from Luff",
        Message = "If you can read this, this channel is wired up correctly.",
    };

    private readonly LuffDbContext _database;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<Unit>
    {
        public Guid Id { get; }

        public Request(Guid id)
        {
            Id = id;
        }
    }

    public TestNotificationChannelHandler(
        LuffDbContext database, INotificationDispatcher dispatcher, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        // Tests a specific channel regardless of its enabled flag, so you can verify wiring before enabling.
        var channel = await _database.NotificationChannels.FindAsync([request.Id], cancellationToken)
            ?? throw new NotificationChannelNotFoundException(request.Id);

        var url = _protector.Unprotect(channel.Url);
        _dispatcher.Enqueue(new NotificationDelivery(url, NotificationFormat.Build(channel.Type, TestEvent)));

        return Unit.Value;
    }
}

public static class TestNotificationChannelHandlerExtensions
{
    public static async Task TestNotificationChannel(
        this ISender sender, Guid id, CancellationToken cancellationToken = default)
    {
        await sender.Send(new TestNotificationChannelHandler.Request(id), cancellationToken);
    }
}
