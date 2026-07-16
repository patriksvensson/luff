using System.Data.Common;
using Luff.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Server.Tests;

internal static class TestOptions
{
    private static readonly DateTimeOffset Epoch = new(2026, 01, 01, 0, 0, 0, TimeSpan.Zero);

    public static DbContextOptions<LuffDbContext> For(DbConnection connection, TimeProvider? time = null)
    {
        return new DbContextOptionsBuilder<LuffDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditInterceptor(time ?? new FakeTimeProvider(Epoch)))
            .Options;
    }
}
