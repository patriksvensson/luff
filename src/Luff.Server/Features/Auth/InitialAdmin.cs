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
        if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        var database = scope.ServiceProvider.GetRequiredService<LuffDbContext>();
        if (await database.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        database.Users.Add(new User
        {
            Username = options.Username,
            PasswordHash = PasswordHasher.Hash(options.Password),
            Role = UserRole.Admin,
            MustChangePassword = true,
        });

        await database.SaveChangesAsync(cancellationToken);
    }
}
