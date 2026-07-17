namespace Luff.Server.Features;

public sealed class ListAuditHandler : IRequestHandler<ListAuditHandler.Request, IReadOnlyList<AuditEventResponse>>
{
    private const int Recent = 50;

    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<AuditEventResponse>>;

    public ListAuditHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<AuditEventResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        // SQLite cannot ORDER BY a DateTimeOffset, so order and cap client-side, the same way the deployment
        // activity view did. A Postgres provider swap can push this back down into the query.
        var events = await _database.AuditEvents.ToListAsync(cancellationToken);

        return
        [
            .. events
                .OrderByDescending(auditEvent => auditEvent.CreatedAt)
                .Take(Recent)
                .Select(auditEvent => auditEvent.ToResponse()),
        ];
    }
}

public static class ListAuditHandlerExtensions
{
    public static async Task<IReadOnlyList<AuditEventResponse>> ListAudit(
        this ISender sender, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListAuditHandler.Request(), cancellationToken);
    }
}
