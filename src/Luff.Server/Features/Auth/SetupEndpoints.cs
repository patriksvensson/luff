namespace Luff.Server.Features;

public static class SetupEndpoints
{
    public static RouteGroupBuilder MapSetupEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/setup", Create)
            .WithName("Setup_Create")
            .WithTags("Setup")
            .AllowAnonymous()
            .RequireRateLimiting(JwtAuth.CredentialsPolicy)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<NoContent> Create(
        SetupRequest request, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Setup(
            request.Username, request.Password, request.Email,
            request.FirstName, request.LastName, cancellationToken);
        return TypedResults.NoContent();
    }
}
