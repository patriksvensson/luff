namespace Luff.Server.Features;

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<Unit>
    {
        public string Username { get; }
        public string CurrentPassword { get; }
        public string NewPassword { get; }

        public Request(string username, string currentPassword, string newPassword)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            CurrentPassword = currentPassword ?? throw new ArgumentNullException(nameof(currentPassword));
            NewPassword = newPassword ?? throw new ArgumentNullException(nameof(newPassword));
        }
    }

    public ChangePasswordHandler(LuffDbContext database, RefreshTokenService refreshTokens)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Username], cancellationToken)
            ?? throw new InvalidCredentialsException();

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        await _database.SaveChangesAsync(cancellationToken);

        await _refreshTokens.RevokeAllAsync(user.Username, cancellationToken);

        return Unit.Value;
    }
}

public static class ChangePasswordHandlerExtensions
{
    public static async Task ChangePassword(
        this ISender sender, string username, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(
            new ChangePasswordHandler.Request(username, currentPassword, newPassword), cancellationToken);
    }
}
