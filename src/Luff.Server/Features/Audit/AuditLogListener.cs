namespace Luff.Server.Features;

// Persists every event as an audit-log row. It opens its own scope so the write is a self-contained unit of
// work: it never flushes the pending changes of whatever handler happened to raise the event, and a failure
// here is logged by the publisher rather than rolling back the triggering operation.
public sealed class AuditLogListener : IEventListener
{
    private readonly IServiceScopeFactory _scopes;

    public AuditLogListener(IServiceScopeFactory scopes)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    public async Task OnEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LuffDbContext>();

        database.AuditEvents.Add(auditEvent);
        await database.SaveChangesAsync(cancellationToken);
    }
}
