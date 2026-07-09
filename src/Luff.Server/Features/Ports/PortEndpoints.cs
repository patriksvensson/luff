namespace Luff.Server.Features;

public static class PortEndpoints
{
    public static RouteGroupBuilder MapPortEndpoints(this RouteGroupBuilder group)
    {
        var ports = group
            .MapGroup("/apps/{name}/ports")
            .WithTags("Ports")
            .RequireAuthorization(JwtAuth.AdminPolicy);

        ports.MapPost("/", Add)
            .WithName("Ports_Add")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        ports.MapGet("/", List)
            .WithName("Ports_List")
            .ProducesProblem(StatusCodes.Status404NotFound);

        ports.MapDelete("/", Remove)
            .WithName("Ports_Remove")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<PortMappingResponse>> Add(
        string name, AddPortRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var mapping = await sender.AddPort(name, request.HostPort, request.ContainerPort, cancellationToken);
        return TypedResults.Ok(mapping);
    }

    private static async Task<Ok<IReadOnlyList<PortMappingResponse>>> List(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var mappings = await sender.ListPorts(name, cancellationToken);
        return TypedResults.Ok(mappings);
    }

    private static async Task<NoContent> Remove(
        string name, int hostPort, ISender sender, CancellationToken cancellationToken)
    {
        await sender.RemovePort(name, hostPort, cancellationToken);
        return TypedResults.NoContent();
    }
}
