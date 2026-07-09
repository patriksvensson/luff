namespace Luff.Server.Persistence;

public sealed class LuffDbContextFactory : IDesignTimeDbContextFactory<LuffDbContext>
{
    public LuffDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LuffDbContext>()
            .UseSqlite("Data Source=luff.db")
            .Options;

        return new LuffDbContext(options);
    }
}
