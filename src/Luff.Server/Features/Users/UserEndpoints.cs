namespace Luff.Server.Features;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        var users = group
            .MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization(JwtAuth.AdminPolicy);

        users.MapPost("/", Create)
            .WithName("Users_Create")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapPost("/{username}/2fa/reset", ResetTwoFactor)
            .WithName("Users_ResetTwoFactor")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<UserResponse>> Create(
        CreateUserRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var user = await sender.CreateUser(request.Username, request.Password, request.Role, cancellationToken);
        return TypedResults.Created($"/api/v1/users/{user.Username}", user);
    }

    private static async Task<NoContent> ResetTwoFactor(
        string username, ISender sender, CancellationToken cancellationToken)
    {
        await sender.ResetUserTwoFactor(username, cancellationToken);
        return TypedResults.NoContent();
    }
}
