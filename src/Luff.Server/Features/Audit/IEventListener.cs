namespace Luff.Server.Features;

// One consumer of raised events. The publisher fans every event out to all registered listeners, isolating
// each so a slow or failing one never takes another down. Add a listener to react to events in a new way.
public interface IEventListener
{
    Task OnEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
