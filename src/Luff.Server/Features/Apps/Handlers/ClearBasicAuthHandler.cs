namespace Luff.Server.Features;

public sealed class ClearBasicAuthHandler : IRequestHandler<ClearBasicAuthHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly ISecretProtector _protector;
    private readonly IBasicAuthHasher _basicAuthHasher;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string Name { get; }
        public string Actor { get; }

        public Request(string name, string actor)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public ClearBasicAuthHandler(
        LuffDbContext database, IAgentConnections connections,
        ISecretProtector protector, IBasicAuthHasher basicAuthHasher, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _basicAuthHasher = basicAuthHasher ?? throw new ArgumentNullException(nameof(basicAuthHasher));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        if (string.IsNullOrEmpty(app.BasicAuthUsername) && string.IsNullOrEmpty(app.BasicAuthPassword))
        {
            // Nothing to clear; keep the delete idempotent (no route churn, no event).
            return Unit.Value;
        }

        app.BasicAuthUsername = null;
        app.BasicAuthPassword = null;
        await _database.SaveChangesAsync(cancellationToken);

        await BasicAuthRouting.ReassertAsync(_database, _connections, _basicAuthHasher, _protector, app, cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AppUpdated,
            Actor = request.Actor,
            Title = $"App updated: {app.Name}",
            Message = $"Basic authentication was disabled for {app.Name}.",
            App = app.Name,
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class ClearBasicAuthHandlerExtensions
{
    public static async Task ClearBasicAuth(
        this ISender sender, string name, string actor, CancellationToken cancellationToken = default)
    {
        await sender.Send(new ClearBasicAuthHandler.Request(name, actor), cancellationToken);
    }
}
