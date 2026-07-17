namespace Luff.Server.Features;

public sealed class RemoveRegistryHandler : IRequestHandler<RemoveRegistryHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string Host { get; }
        public string Actor { get; }

        public Request(string host, string actor)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public RemoveRegistryHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var registry = await _database.Registries.FindAsync([request.Host], cancellationToken)
            ?? throw new RegistryNotFoundException(request.Host);

        _database.Registries.Remove(registry);
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.RegistryRemoved,
            Actor = request.Actor,
            Title = $"Registry removed: {registry.Host}",
            Message = $"Registry '{registry.Host}' credentials were removed.",
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class RemoveRegistryHandlerExtensions
{
    public static async Task RemoveRegistry(this ISender sender, string host, string actor,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveRegistryHandler.Request(host, actor), cancellationToken);
    }
}
