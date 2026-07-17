namespace Luff.Server.Features;

public sealed class RegenerateRecoveryCodesHandler
    : IRequestHandler<RegenerateRecoveryCodesHandler.Request, RecoveryCodesResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<RecoveryCodesResponse>
    {
        public string Email { get; }

        public Request(string email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }

    public RegenerateRecoveryCodesHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<RecoveryCodesResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        if (!user.TwoFactorEnabled)
        {
            throw new TwoFactorNotEnabledException();
        }

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

        return new RecoveryCodesResponse(codes);
    }
}

public static class RegenerateRecoveryCodesHandlerExtensions
{
    public static async Task<RecoveryCodesResponse> RegenerateRecoveryCodes(
        this ISender sender, string email, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new RegenerateRecoveryCodesHandler.Request(email), cancellationToken);
    }
}
