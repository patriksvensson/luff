using System.Globalization;
using Luff.Protobuf;

namespace Luff.Agent;

public sealed class AgentLogStreamer
{
    private readonly IDockerComposeRunner _dockerCompose;

    public AgentLogStreamer(IDockerComposeRunner dockerCompose)
    {
        _dockerCompose = dockerCompose ?? throw new ArgumentNullException(nameof(dockerCompose));
    }

    public Task StreamAsync(
        string streamId,
        string app,
        int tail,
        Action<AgentMessage> emit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emit);

        return _dockerCompose.StreamLogsAsync(
            app,
            tail,
            line => emit(new AgentMessage
            {
                LogChunk = new LogChunk
                {
                    StreamId = streamId,
                    Stream = line.Stream == DockerLogStreamKind.Stderr
                        ? LogStreamKind.Stderr
                        : LogStreamKind.Stdout,
                    Line = line.Text,
                    Timestamp = line.Timestamp?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                },
            }),
            cancellationToken);
    }
}
