namespace Luff.Server.Features;

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserHandler.Request, UserResponse>
{
    private readonly LuffDbContext _database;
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<UserResponse>
    {
        public string Email { get; }
        public string Role { get; }
        public string? FirstName { get; }
        public string? LastName { get; }
        public string? NewPassword { get; }

        public Request(
            string email, string role,
            string? firstName = null, string? lastName = null, string? newPassword = null)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Role = role ?? throw new ArgumentNullException(nameof(role));
            FirstName = firstName;
            LastName = lastName;
            NewPassword = newPassword;
        }
    }

    public UpdateUserHandler(LuffDbContext database, RefreshTokenService refreshTokens)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<UserResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new InvalidUserRoleException(request.Role);
        }

        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        if (user.Role == UserRole.Admin && role != UserRole.Admin && await IsLastAdmin(cancellationToken))
        {
            throw new LastAdminException();
        }

        user.Role = role;
        user.FirstName = Clean(request.FirstName);
        user.LastName = Clean(request.LastName);

        var resetPassword = !string.IsNullOrEmpty(request.NewPassword);
        if (resetPassword)
        {
            user.PasswordHash = PasswordHasher.Hash(request.NewPassword!);
        }

        await _database.SaveChangesAsync(cancellationToken);

        // A password change invalidates the user's live sessions, forcing a fresh sign-in with the new one.
        if (resetPassword)
        {
            await _refreshTokens.RevokeAllAsync(user.Email, cancellationToken);
        }

        return user.ToResponse();
    }

    private async Task<bool> IsLastAdmin(CancellationToken cancellationToken) =>
        await _database.Users.CountAsync(entry => entry.Role == UserRole.Admin, cancellationToken) <= 1;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class UpdateUserHandlerExtensions
{
    public static async Task<UserResponse> UpdateUser(
        this ISender sender, string email, string role,
        string? firstName = null, string? lastName = null, string? newPassword = null,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new UpdateUserHandler.Request(email, role, firstName, lastName, newPassword),
            cancellationToken);
    }
}
