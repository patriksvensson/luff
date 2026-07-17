namespace Luff.Server.Features;

public sealed class RefreshTokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly LuffDbContext _database;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenService(LuffDbContext database, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<string> IssueAsync(string email, CancellationToken cancellationToken)
    {
        return await CreateAsync(email, Guid.NewGuid(), _timeProvider.GetUtcNow().Add(Lifetime), cancellationToken);
    }

    public async Task<(string Token, string Email)> RotateAsync(string presented, CancellationToken cancellationToken)
    {
        var hash = RefreshToken.Hash(presented);
        var token = await _database.RefreshTokens.FirstOrDefaultAsync(
            entry => entry.TokenHash == hash, cancellationToken)
            ?? throw new InvalidCredentialsException();

        var now = _timeProvider.GetUtcNow();

        if (token.ConsumedAt is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (token.RevokedAt is not null || token.ExpiresAt <= now)
        {
            throw new InvalidCredentialsException();
        }

        token.ConsumedAt = now;
        var next = await CreateAsync(token.Email, token.FamilyId, token.ExpiresAt, cancellationToken);
        return (next, token.Email);
    }

    public async Task RevokeByTokenAsync(string presented, CancellationToken cancellationToken)
    {
        var hash = RefreshToken.Hash(presented);
        var token = await _database.RefreshTokens.FirstOrDefaultAsync(
            entry => entry.TokenHash == hash, cancellationToken);

        if (token is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, cancellationToken);
        }
    }

    public async Task RevokeAllAsync(string email, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var tokens = await _database.RefreshTokens
            .Where(entry => entry.Email == email && entry.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var tokens = await _database.RefreshTokens
            .Where(entry => entry.FamilyId == familyId && entry.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> CreateAsync(
        string email, Guid familyId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var token = RefreshToken.Generate();

        _database.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Email = email,
            FamilyId = familyId,
            TokenHash = RefreshToken.Hash(token),
            ExpiresAt = expiresAt,
        });

        await _database.SaveChangesAsync(cancellationToken);
        return token;
    }
}
