using Luff.Server.Features;
using Luff.Server.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using AgentEntity = Luff.Server.Features.Agent;

namespace Luff.Server.Tests.Fleet;

public sealed class AuthenticateAgentFixture : IDisposable
{
    public const string BootstrapSecret = "boot-secret";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly AgentEnrollmentValidator _validator;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 07, 02, 12, 0, 0, TimeSpan.Zero));

    public AuthenticateAgentFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection, _time);

        using var context = CreateContext();
        context.Database.EnsureCreated();

        _validator = new AgentEnrollmentValidator(
            Options.Create(new AgentEnrollmentOptions { Secret = BootstrapSecret }));
    }

    public async Task<bool> Authenticate(string name, string? secret)
    {
        await using var context = CreateContext();
        var handler = new AuthenticateAgentHandler(context, _validator, _time);
        return await handler.Handle(new AuthenticateAgentHandler.Request(name, secret), CancellationToken.None);
    }

    public async Task HasTokenAgent(string name, string token)
    {
        await using var context = CreateContext();
        context.Agents.Add(new AgentEntity
        {
            Name = name,
            EnrollmentTokenHash = AgentEntity.Hash(token),
            RegisteredAt = _time.GetUtcNow(),
        });
        await context.SaveChangesAsync();
    }

    public async Task<AgentEntity?> GetAgent(string name)
    {
        await using var context = CreateContext();
        return await context.Agents.FindAsync(name);
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
