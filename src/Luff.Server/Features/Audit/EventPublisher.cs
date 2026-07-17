namespace Luff.Server.Features;

public sealed class EventPublisher : IEventPublisher
{
    private readonly IEnumerable<IEventListener> _listeners;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IEnumerable<IEventListener> listeners, ILogger<EventPublisher> logger)
    {
        _listeners = listeners ?? throw new ArgumentNullException(nameof(listeners));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        auditEvent.Id = Guid.CreateVersion7();

        foreach (var listener in _listeners)
        {
            try
            {
                await listener.OnEventAsync(auditEvent, cancellationToken);
            }
            catch (Exception exception)
            {
                // A listener is a side effect: one failing must never break the operation that raised the
                // event, nor stop the other listeners from seeing it.
                _logger.LogWarning(exception, "Event listener {Listener} failed for {Kind}",
                    listener.GetType().Name, auditEvent.Kind);
            }
        }
    }
}
