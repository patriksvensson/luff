namespace Luff.Server.Features;

public sealed class ListRegistriesHandler
    : IRequestHandler<ListRegistriesHandler.Request, IReadOnlyList<RegistryResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<RegistryResponse>>
    {
    }

    public ListRegistriesHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<RegistryResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var registries = await _database.Registries
            .OrderBy(registry => registry.Host)
            .ToListAsync(cancellationToken);

        return [.. registries.Select(registry => registry.ToResponse())];
    }
}

public static class ListRegistriesHandlerExtensions
{
    public static async Task<IReadOnlyList<RegistryResponse>> ListRegistries(
        this ISender sender, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListRegistriesHandler.Request(), cancellationToken);
    }
}
