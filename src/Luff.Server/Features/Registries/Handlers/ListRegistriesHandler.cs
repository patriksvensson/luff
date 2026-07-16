namespace Luff.Server.Features;

public sealed class ListRegistriesHandler
    : IRequestHandler<ListRegistriesHandler.Request, IReadOnlyList<RegistryResponse>>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<IReadOnlyList<RegistryResponse>>
    {
    }

    public ListRegistriesHandler(LuffDbContext database, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<IReadOnlyList<RegistryResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var registries = await _database.Registries
            .OrderBy(registry => registry.Host)
            .ToListAsync(cancellationToken);

        return [.. registries.Select(registry => registry.ToResponse(_protector.Unprotect(registry.Password)))];
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
