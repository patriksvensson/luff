using System.Runtime.CompilerServices;

namespace Luff.Server.Features;

public interface ILogStream
{
    IAsyncEnumerable<LogEvent> Tail(string agent, string app, int tail, CancellationToken cancellationToken = default);

    void PublishChunk(Guid streamId, LogEvent logEvent);
}

public sealed class LogStream : ILogStream
{
    private readonly IAgentConnections _connections;
    private readonly ConcurrentDictionary<Guid, Channel<LogEvent>> _streams = new();

    public LogStream(IAgentConnections connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public async IAsyncEnumerable<LogEvent> Tail(
        string agent,
        string app,
        int tail,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<LogEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        _streams[streamId] = channel;
        _connections.TrySend(agent, new ControlMessage
        {
            StartLogStream = new StartLogStream
            {
                StreamId = streamId.ToString(),
                App = app,
                Tail = tail,
            },
        });

        try
        {
            await foreach (var logEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return logEvent;
            }
        }
        finally
        {
            _streams.TryRemove(streamId, out _);
            _connections.TrySend(agent, new ControlMessage
            {
                StopLogStream = new StopLogStream
                {
                    StreamId = streamId.ToString(),
                },
            });
        }
    }

    public void PublishChunk(Guid streamId, LogEvent logEvent)
    {
        if (_streams.TryGetValue(streamId, out var channel))
        {
            channel.Writer.TryWrite(logEvent);
        }
    }
}
