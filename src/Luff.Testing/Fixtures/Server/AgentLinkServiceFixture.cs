using Luff.Protobuf;
using Luff.Server.Features;
using Luff.Server.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Luff.Server.Tests.Fleet;

public sealed class AgentLinkServiceFixture
{
    public const string ValidSecret = "correct-horse-battery-staple";

    private readonly AgentLinkService _service;

    public AgentRegistry Registry { get; } = new();
    public FakeScopedSender Sender { get; } = new();
    public DeployEvents Events { get; } = new();
    public FakeAgentConnections Connections { get; } = new();
    public FakeHostApplicationLifetime Lifetime { get; } = new();
    public LogStream Logs { get; }

    public AgentLinkServiceFixture()
    {
        Sender.RespondTo<AuthenticateAgentHandler.Request, bool>(
            request => request.PresentedSecret == ValidSecret);

        Logs = new LogStream(Connections);
        _service = new AgentLinkService(
            Sender, Registry, Connections, Events,
            new FleetEvents(), Logs, Lifetime, NullLogger<AgentLinkService>.Instance);
    }

    public static AgentMessage Hello(
        string name, string secret, string version = "1.0.0", bool hostsFrontDoor = false)
    {
        return new AgentMessage
        {
            Hello = new Hello
            {
                AgentName = name,
                EnrollmentSecret = secret,
                Version = version,
                HostsFrontDoor = hostsFrontDoor,
            },
        };
    }

    public async Task<IReadOnlyList<ControlMessage>> Connect(params AgentMessage[] frames)
    {
        var reader = new FakeAsyncStreamReader<AgentMessage>(frames);
        var writer = new FakeServerStreamWriter<ControlMessage>();

        await _service.Connect(reader, writer, CreateContext());

        return writer.Written;
    }

    public (Task Running, FakeAsyncStreamReader<AgentMessage> Reader) ConnectOpenEnded(params AgentMessage[] frames)
    {
        var reader = new FakeAsyncStreamReader<AgentMessage>(frames, keepOpen: true);
        var writer = new FakeServerStreamWriter<ControlMessage>();

        var running = _service.Connect(reader, writer, CreateContext());

        return (running, reader);
    }

    private static FakeServerCallContext CreateContext()
    {
        return new FakeServerCallContext(CancellationToken.None);
    }
}
