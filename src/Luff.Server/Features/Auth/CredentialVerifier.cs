namespace Luff.Server.Features;

public sealed class CredentialVerifier
{
    private readonly LuffDbContext _database;

    public CredentialVerifier(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<User> VerifyAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([username], cancellationToken);

        var valid = PasswordHasher.Verify(password, user?.PasswordHash ?? PasswordHasher.Dummy);
        if (user is null || !valid)
        {
            throw new InvalidCredentialsException();
        }

        return user;
    }
}
