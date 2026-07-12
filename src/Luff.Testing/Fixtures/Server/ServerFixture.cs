using System.Threading.Channels;
using Luff.Protobuf;
using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Luff.Server.Tests.Server;

public sealed class ServerFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly FrontDoorOptions _frontDoorOptions = new()
    {
        Domain = "127.0.0.1.sslip.io",
        Upstream = "host.docker.internal:8080",
    };

    private readonly DirectoryInfo _keys = Directory.CreateTempSubdirectory("luff-server-fixture");
    private readonly AgentLinkCertificate _certificate;

    public FakeAgentConnections Agents { get; }
    public AgentRegistry Registry { get; } = new();
    public FrontDoorConfigurator FrontDoor { get; }

    public ServerFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LuffDbContext>()
            .UseSqlite(_connection)
            .Options;

        Agents = new FakeAgentConnections();
        FrontDoor = new FrontDoorConfigurator(Agents, Registry, Options.Create(new FrontDoorOptions
        {
            Domain = "127.0.0.1.sslip.io",
            Upstream = "host.docker.internal:8080",
        }));

        _certificate = AgentLinkCertificate.Resolve(_keys.FullName);

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ChannelReader<ControlMessage> HasFrontDoorAgent(string name)
    {
        Registry.MarkConnected(name, "1.0.0", hostsFrontDoor: true);
        return Agents.Register(name);
    }

    public async Task<ServerResponse> GetServer()
    {
        var handler = new GetServerHandler(CreateContext(), Options.Create(_frontDoorOptions), _certificate);
        return await handler.Handle(new GetServerHandler.Request(), CancellationToken.None);
    }

    public async Task<ServerResponse> SetDomain(string domain)
    {
        var handler = new SetFrontDoorDomainHandler(CreateContext(), FrontDoor, _certificate);
        return await handler.Handle(new SetFrontDoorDomainHandler.Request(domain), CancellationToken.None);
    }

    public async Task<ServerResponse> SetAgentLink(string address)
    {
        var handler = new SetAgentLinkAddressHandler(CreateContext(), Options.Create(_frontDoorOptions), _certificate);
        return await handler.Handle(new SetAgentLinkAddressHandler.Request(address), CancellationToken.None);
    }

    public async Task<ServerSettings?> GetSettingsFromDatabase()
    {
        await using var context = CreateContext();
        return await context.ServerSettings.FirstOrDefaultAsync();
    }

    public void Dispose()
    {
        _certificate.Certificate.Dispose();
        _connection.Dispose();
        _keys.Delete(recursive: true);
    }

    private LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }
}
