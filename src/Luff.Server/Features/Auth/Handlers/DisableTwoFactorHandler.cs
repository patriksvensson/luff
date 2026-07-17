namespace Luff.Server.Features;

public sealed class DisableTwoFactorHandler : IRequestHandler<DisableTwoFactorHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly TwoFactorService _twoFactor;
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<Unit>
    {
        public string Email { get; }
        public string Code { get; }

        public Request(string email, string code)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }
    }

    public DisableTwoFactorHandler(
        LuffDbContext database, TwoFactorService twoFactor, RefreshTokenService refreshTokens)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _twoFactor = twoFactor ?? throw new ArgumentNullException(nameof(twoFactor));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        if (!user.TwoFactorEnabled)
        {
            throw new TwoFactorNotEnabledException();
        }

        if (!await _twoFactor.VerifyAsync(user, request.Code, cancellationToken))
        {
            throw new InvalidTwoFactorCodeException();
        }

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

public static class DisableTwoFactorHandlerExtensions
{
    public static async Task DisableTwoFactor(
        this ISender sender, string email, string code, CancellationToken cancellationToken = default)
    {
        await sender.Send(new DisableTwoFactorHandler.Request(email, code), cancellationToken);
    }
}
