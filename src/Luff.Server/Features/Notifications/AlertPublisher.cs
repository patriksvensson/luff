namespace Luff.Server.Features;

public interface IAlertPublisher
{
    Task PublishAsync(Alert alert, CancellationToken cancellationToken = default);
}

public sealed class AlertPublisher : IAlertPublisher
{
    private readonly LuffDbContext _database;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ISecretProtector _protector;
    private readonly ILogger<AlertPublisher> _logger;

    public AlertPublisher(
        LuffDbContext database,
        INotificationDispatcher dispatcher,
        ISecretProtector protector,
        ILogger<AlertPublisher> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        try
        {
            var channels = await _database.NotificationChannels
                .Where(channel => channel.Enabled)
                .ToListAsync(cancellationToken);

            foreach (var channel in channels)
            {
                var url = _protector.Unprotect(channel.Url);
                _dispatcher.Enqueue(new NotificationDelivery(url, NotificationFormat.Build(channel.Type, alert)));
            }
        }
        catch (Exception exception)
        {
            // Alerting is best-effort: never let it break the operation that raised the alert.
            _logger.LogWarning(exception, "Failed to publish alert {Kind}", alert.Kind);
        }
    }
}
