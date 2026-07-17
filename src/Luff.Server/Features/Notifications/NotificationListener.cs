namespace Luff.Server.Features;

// Fans a raised event out to every enabled notification channel. Reads channels from the scoped context (a
// read never flushes the caller's pending changes) and hands each formatted payload to the dispatcher, which
// delivers out-of-band so a slow endpoint never blocks the operation.
public sealed class NotificationListener : IEventListener
{
    private readonly LuffDbContext _database;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ISecretProtector _protector;

    public NotificationListener(
        LuffDbContext database,
        INotificationDispatcher dispatcher,
        ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task OnEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var channels = await _database.NotificationChannels
            .Where(channel => channel.Enabled)
            .ToListAsync(cancellationToken);

        foreach (var channel in channels)
        {
            var url = _protector.Unprotect(channel.Url);
            _dispatcher.Enqueue(new NotificationDelivery(url, NotificationFormat.Build(channel.Type, auditEvent)));
        }
    }
}
