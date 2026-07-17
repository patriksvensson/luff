namespace Luff.Server.Features;

public sealed class ConfirmTwoFactorEnrollmentHandler
    : IRequestHandler<ConfirmTwoFactorEnrollmentHandler.Request, RecoveryCodesResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;
    private readonly RefreshTokenService _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public sealed class Request : IRequest<RecoveryCodesResponse>
    {
        public string Email { get; }
        public string Code { get; }

        public Request(string email, string code)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }
    }

    public ConfirmTwoFactorEnrollmentHandler(
        LuffDbContext database, ISecretProtector protector, RefreshTokenService refreshTokens,
        TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RecoveryCodesResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        if (user.TwoFactorEnabled)
        {
            throw new TwoFactorAlreadyEnabledException();
        }

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            throw new TwoFactorNotEnabledException();
        }

        var secret = _protector.Unprotect(user.TwoFactorSecret);
        if (!Totp.Verify(secret, request.Code, _timeProvider.GetUtcNow()))
        {
            throw new InvalidTwoFactorCodeException();
        }

        user.TwoFactorEnabled = true;

        var stale = await _database.RecoveryCodes
            .Where(code => code.Email == user.Email)
            .ToListAsync(cancellationToken);
        _database.RecoveryCodes.RemoveRange(stale);

        var codes = TwoFactorService.GenerateRecoveryCodes();
        foreach (var code in codes)
        {
            _database.RecoveryCodes.Add(new RecoveryCode
            {
                Id = Guid.NewGuid(),
                Email = user.Email,
                CodeHash = RecoveryCode.Hash(code),
            });
        }

        await _database.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(user.Email, cancellationToken);

        return new RecoveryCodesResponse(codes);
    }
}

public static class ConfirmTwoFactorEnrollmentHandlerExtensions
{
    public static async Task<RecoveryCodesResponse> ConfirmTwoFactorEnrollment(
        this ISender sender, string email, string code, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ConfirmTwoFactorEnrollmentHandler.Request(email, code), cancellationToken);
    }
}
