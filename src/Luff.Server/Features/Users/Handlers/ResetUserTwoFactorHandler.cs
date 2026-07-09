namespace Luff.Server.Features;

public sealed class ResetUserTwoFactorHandler : IRequestHandler<ResetUserTwoFactorHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<Unit>
    {
        public string Username { get; }

        public Request(string username)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
        }
    }

    public ResetUserTwoFactorHandler(LuffDbContext database, RefreshTokenService refreshTokens)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Username], cancellationToken)
            ?? throw new UserNotFoundException(request.Username);

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;

        var codes = await _database.RecoveryCodes
            .Where(code => code.Username == user.Username)
            .ToListAsync(cancellationToken);
        _database.RecoveryCodes.RemoveRange(codes);

        await _database.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(user.Username, cancellationToken);

        return Unit.Value;
    }
}

public static class ResetUserTwoFactorHandlerExtensions
{
    public static async Task ResetUserTwoFactor(
        this ISender sender, string username, CancellationToken cancellationToken = default)
    {
        await sender.Send(new ResetUserTwoFactorHandler.Request(username), cancellationToken);
    }
}
