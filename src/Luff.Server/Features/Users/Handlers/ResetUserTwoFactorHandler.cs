namespace Luff.Server.Features;

public sealed class ResetUserTwoFactorHandler : IRequestHandler<ResetUserTwoFactorHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<Unit>
    {
        public string Email { get; }

        public Request(string email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }

    public ResetUserTwoFactorHandler(LuffDbContext database, RefreshTokenService refreshTokens)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;

        var codes = await _database.RecoveryCodes
            .Where(code => code.Email == user.Email)
            .ToListAsync(cancellationToken);
        _database.RecoveryCodes.RemoveRange(codes);

        await _database.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(user.Email, cancellationToken);

        return Unit.Value;
    }
}

public static class ResetUserTwoFactorHandlerExtensions
{
    public static async Task ResetUserTwoFactor(
        this ISender sender, string email, CancellationToken cancellationToken = default)
    {
        await sender.Send(new ResetUserTwoFactorHandler.Request(email), cancellationToken);
    }
}
