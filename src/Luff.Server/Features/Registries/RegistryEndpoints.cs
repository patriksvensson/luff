namespace Luff.Server.Features;

public static class RegistryEndpoints
{
    public static RouteGroupBuilder MapRegistryEndpoints(this RouteGroupBuilder group)
    {
        var registries = group
            .MapGroup("/registries")
            .WithTags("Registries")
            .RequireAuthorization(JwtAuth.AdminPolicy);

        registries.MapPost("/", Add)
            .WithName("Registries_Add");

        registries.MapGet("/", List)
            .WithName("Registries_List");

        registries.MapDelete("/{host}", Remove)
            .WithName("Registries_Remove")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<RegistryResponse>> Add(
        AddRegistryRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var registry = await sender.AddRegistry(request.Host, request.Username, request.Password, cancellationToken);
        return TypedResults.Ok(registry);
    }

    private static async Task<Ok<IReadOnlyList<RegistryResponse>>> List(
        ISender sender, CancellationToken cancellationToken)
    {
        var registries = await sender.ListRegistries(cancellationToken);
        return TypedResults.Ok(registries);
    }

    private static async Task<NoContent> Remove(
        string host, ISender sender, CancellationToken cancellationToken)
    {
        await sender.RemoveRegistry(host, cancellationToken: cancellationToken);
        return TypedResults.NoContent();
    }
}
