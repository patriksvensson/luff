namespace Luff.Server.Features;

public static class VolumeEndpoints
{
    public static RouteGroupBuilder MapVolumeEndpoints(this RouteGroupBuilder group)
    {
        var volumes = group
            .MapGroup("/apps/{name}/volumes")
            .WithTags("Volumes")
            .RequireAuthorization(JwtAuth.AdminPolicy);

        volumes.MapPost("/", Add)
            .WithName("Volumes_Add")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        volumes.MapGet("/", List)
            .WithName("Volumes_List")
            .ProducesProblem(StatusCodes.Status404NotFound);

        volumes.MapDelete("/", Remove)
            .WithName("Volumes_Remove")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<VolumeResponse>> Add(
        string name, AddVolumeRequest request, ClaimsPrincipal principal, ISender sender,
        CancellationToken cancellationToken)
    {
        var actor = principal.Identity?.Name ?? Actors.System;
        var volume = await sender.AddVolume(
            name, request.Source, request.Target, request.ReadOnly, actor, cancellationToken);

        return TypedResults.Ok(volume);
    }

    private static async Task<Ok<IReadOnlyList<VolumeResponse>>> List(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var volumes = await sender.ListVolumes(name, cancellationToken);
        return TypedResults.Ok(volumes);
    }

    private static async Task<NoContent> Remove(
        string name, string target, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        await sender.RemoveVolume(name, target, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.NoContent();
    }
}
