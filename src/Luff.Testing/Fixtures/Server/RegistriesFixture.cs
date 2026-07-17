using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Luff.Server.Tests.Registries;

public sealed class RegistriesFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;

    public ISecretProtector Protector { get; } = new FakeSecretProtector();
    public FakeEventPublisher Events { get; } = new();

    public RegistriesFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection);

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public async Task<RegistryResponse> AddRegistry(
        string host, string username, string password, string actor = "admin@example.com")
    {
        var handler = new AddRegistryHandler(CreateContext(), Protector, Events);
        return await handler.Handle(
            new AddRegistryHandler.Request(host, username, password, actor),
            CancellationToken.None);
    }

    public async Task<IReadOnlyList<RegistryResponse>> ListRegistries()
    {
        var handler = new ListRegistriesHandler(CreateContext(), Protector);
        return await handler.Handle(new ListRegistriesHandler.Request(), CancellationToken.None);
    }

    public async Task RemoveRegistry(string host, string actor = "admin@example.com")
    {
        var handler = new RemoveRegistryHandler(CreateContext(), Events);
        await handler.Handle(new RemoveRegistryHandler.Request(host, actor), CancellationToken.None);
    }

    public async Task<IReadOnlyList<Registry>> GetRegistries()
    {
        await using var context = CreateContext();
        return await context.Registries.ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }
}