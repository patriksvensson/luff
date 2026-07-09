namespace Luff.Server.Features;

public static class StatusEndpoints
{
    public static RouteGroupBuilder MapStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/apps/{name}/status", Show)
            .WithName("Status_Show")
            .WithTags("Status")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<AppStatusHandler.Response>> Show(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var status = await sender.AppStatus(name, cancellationToken);
        return TypedResults.Ok(status);
    }
}
