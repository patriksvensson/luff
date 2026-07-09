using Luff.Server.Features;
using Luff.Server.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Luff.Server.Tests.Ports;

public sealed class PortsFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;

    public PortsFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LuffDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }

    public async Task<PortMappingResponse> AddPort(AddPortHandler.Request request)
    {
        var handler = new AddPortHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<IReadOnlyList<PortMappingResponse>> ListPorts(ListPortsHandler.Request request)
    {
        var handler = new ListPortsHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task RemovePort(RemovePortHandler.Request request)
    {
        var handler = new RemovePortHandler(CreateContext());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task HasApp(string name = "tool", AppKind kind = AppKind.Direct)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Kind = kind,
            Image = "grafana",
            Domain = kind == AppKind.Web ? $"{name}.example.com" : null,
            InternalPort = 3000,
        });

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<PortMapping>> GetPorts(string appName)
    {
        await using var context = CreateContext();

        return await context.PortMappings
            .Where(mapping => mapping.AppName == appName)
            .ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
