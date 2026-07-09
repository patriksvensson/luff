using Luff.Agent.Tests.Fakes;
using Luff.Protobuf;

namespace Luff.Agent.Tests.Fixtures;

public sealed class AgentLogStreamerFixture
{
    public FakeDockerComposeRunner DockerCompose { get; }
    public List<AgentMessage> Emitted { get; } = [];

    public AgentLogStreamerFixture()
    {
        DockerCompose = new FakeDockerComposeRunner(new DockerComposeResult(true, null));
    }

    public async Task StreamAsync(
        string streamId, string app, int tail, CancellationToken cancellationToken = default)
    {
        var streamer = new AgentLogStreamer(DockerCompose);
        await streamer.StreamAsync(streamId, app, tail, message => Emitted.Add(message), cancellationToken);
    }
}
