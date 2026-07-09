namespace Luff.Server.Features;

public sealed class RemoveRegistryHandler : IRequestHandler<RemoveRegistryHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string Host { get; }

        public Request(string host)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }
    }

    public RemoveRegistryHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var registry = await _database.Registries.FindAsync([request.Host], cancellationToken)
            ?? throw new RegistryNotFoundException(request.Host);

        _database.Registries.Remove(registry);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class RemoveRegistryHandlerExtensions
{
    public static async Task RemoveRegistry(this ISender sender, string host,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveRegistryHandler.Request(host), cancellationToken);
    }
}
