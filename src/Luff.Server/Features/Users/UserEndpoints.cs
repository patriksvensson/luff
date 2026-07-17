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

        users.MapPut("/{email}", Update)
            .WithName("Users_Update")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapDelete("/{email}", Delete)
            .WithName("Users_Delete")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapPost("/{email}/2fa/reset", ResetTwoFactor)
            .WithName("Users_ResetTwoFactor")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<UserResponse>> Create(
        CreateUserRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var user = await sender.CreateUser(
            request.Password, request.Role, request.Email,
            request.FirstName, request.LastName, cancellationToken);
        return TypedResults.Created($"/api/v1/users/{user.Email}", user);
    }

    private static async Task<Ok<UserResponse>> Update(
        string email, UpdateUserRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var user = await sender.UpdateUser(
            email, request.Role, request.FirstName, request.LastName, request.NewPassword,
            cancellationToken);
        return TypedResults.Ok(user);
    }

    private static async Task<NoContent> Delete(
        string email, ISender sender, CancellationToken cancellationToken)
    {
        await sender.DeleteUser(email, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ResetTwoFactor(
        string email, ISender sender, CancellationToken cancellationToken)
    {
        await sender.ResetUserTwoFactor(email, cancellationToken);
        return TypedResults.NoContent();
    }
}
