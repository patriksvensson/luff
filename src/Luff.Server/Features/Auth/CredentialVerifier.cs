namespace Luff.Server.Features;

public sealed class CredentialVerifier
{
    private readonly LuffDbContext _database;

    public CredentialVerifier(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<User> VerifyAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = EmailAddress.TryNormalize(email, out var normalized)
            ? await _database.Users.FindAsync([normalized], cancellationToken)
            : null;

        var valid = PasswordHasher.Verify(password, user?.PasswordHash ?? PasswordHasher.Dummy);
        if (user is null || !valid)
        {
            throw new InvalidCredentialsException();
        }

        return user;
    }
}
