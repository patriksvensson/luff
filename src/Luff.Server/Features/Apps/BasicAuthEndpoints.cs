namespace Luff.Server.Features;

public static class BasicAuthEndpoints
{
    public static RouteGroupBuilder MapBasicAuthEndpoints(this RouteGroupBuilder group)
    {
        var basicAuth = group
            .MapGroup("/apps/{name}/basic-auth")
            .WithTags("BasicAuth");

        basicAuth.MapGet("/", Get)
            .WithName("BasicAuth_Get")
            .ProducesProblem(StatusCodes.Status404NotFound);

        basicAuth.MapPut("/", Set)
            .WithName("BasicAuth_Set")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        basicAuth.MapDelete("/", Clear)
            .WithName("BasicAuth_Clear")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<BasicAuthResponse>> Get(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var basicAuth = await sender.GetBasicAuth(name, cancellationToken);
        return TypedResults.Ok(basicAuth);
    }

    private static async Task<NoContent> Set(
        string name, SetBasicAuthRequest request, ClaimsPrincipal principal, ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.SetBasicAuth(
            name, request.Username ?? string.Empty, request.Password ?? string.Empty,
            principal.Identity?.Name ?? Actors.System, cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Clear(
        string name, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        await sender.ClearBasicAuth(name, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.NoContent();
    }
}
