using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Luff.Server.Tests.Env;

public sealed class EnvFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;

    public ISecretProtector Protector { get; } = new FakeSecretProtector();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 07, 17, 12, 0, 0, TimeSpan.Zero));

    public EnvFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection, Time);

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }

    public async Task SetEnv(SetEnvVarHandler.Request request)
    {
        var handler = new SetEnvVarHandler(CreateContext(), Protector);
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task UnsetEnv(UnsetEnvVarHandler.Request request)
    {
        var handler = new UnsetEnvVarHandler(CreateContext());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task<IReadOnlyList<EnvVarResponse>> ListEnv(ListEnvVarsHandler.Request request)
    {
        var handler = new ListEnvVarsHandler(CreateContext(), Protector);
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task HasApp(string name)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Image = "nginx",
            Domain = $"{name}.example.com",
            InternalPort = 80,
        });

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<EnvVar>> GetEnvVars(string appName)
    {
        await using var context = CreateContext();

        return await context.EnvVars
            .Where(env => env.AppName == appName)
            .ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}