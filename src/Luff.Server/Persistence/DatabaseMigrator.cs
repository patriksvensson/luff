namespace Luff.Server.Persistence;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (OpenApiDocumentGeneration.InProgress)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LuffDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }
}
