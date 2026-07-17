namespace Luff.Server.Features;

public sealed class TwoFactorService
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;
    private readonly TimeProvider _timeProvider;

    public TwoFactorService(LuffDbContext database, ISecretProtector protector, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<bool> VerifyAsync(User user, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var secret = _protector.Unprotect(user.TwoFactorSecret);
        if (Totp.Verify(secret, code, _timeProvider.GetUtcNow()))
        {
            return true;
        }

        return await TryConsumeRecoveryCodeAsync(user.Email, code, cancellationToken);
    }

    public static IReadOnlyList<string> GenerateRecoveryCodes(int count = 10)
    {
        var codes = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            codes.Add(RecoveryCode.Generate());
        }

        return codes;
    }

    private async Task<bool> TryConsumeRecoveryCodeAsync(
        string email, string code, CancellationToken cancellationToken)
    {
        var hash = RecoveryCode.Hash(code);
        var entry = await _database.RecoveryCodes.FirstOrDefaultAsync(
            recovery => recovery.Email == email && recovery.CodeHash == hash && recovery.ConsumedAt == null,
            cancellationToken);

        if (entry is null)
        {
            return false;
        }

        entry.ConsumedAt = _timeProvider.GetUtcNow();
        await _database.SaveChangesAsync(cancellationToken);
        return true;
    }
}
