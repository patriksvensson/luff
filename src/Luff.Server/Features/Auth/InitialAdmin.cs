namespace Luff.Server.Features;

public static class InitialAdmin
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Prevent OpenAPI generation from starting seeding stuff
        if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<InitialAdminOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        if (!EmailAddress.TryNormalize(options.Email, out var email))
        {
            throw new InvalidOperationException(
                "Auth:InitialAdmin sets a password but no valid email. " +
                "Set Auth:InitialAdmin:Email (LUFF_ADMIN_EMAIL), or clear the password to use the setup wizard.");
        }

        var database = scope.ServiceProvider.GetRequiredService<LuffDbContext>();
        if (await database.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        database.Users.Add(new User
        {
            PasswordHash = PasswordHasher.Hash(options.Password),
            Role = UserRole.Admin,
            Email = email,
            FirstName = string.IsNullOrWhiteSpace(options.FirstName) ? null : options.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(options.LastName) ? null : options.LastName.Trim(),
        });

        await database.SaveChangesAsync(cancellationToken);
    }
}
