namespace Luff.Server.Features;

// The one place events are raised. Handlers and services depend on this; the fan-out to the audit log and to
// notification channels lives behind it.
public interface IEventPublisher
{
    Task PublishAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
