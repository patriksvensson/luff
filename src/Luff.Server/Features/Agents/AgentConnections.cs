namespace Luff.Server.Features;

public interface IAgentConnections
{
    IReadOnlyCollection<string> Connected { get; }

    ChannelReader<ControlMessage> Register(string name);
    void Unregister(string name);
    bool TrySend(string name, ControlMessage message);
}

public sealed class AgentConnections : IAgentConnections
{
    private readonly ConcurrentDictionary<string, Channel<ControlMessage>> _outbound = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Connected => [.. _outbound.Keys];

    public ChannelReader<ControlMessage> Register(string name)
    {
        var channel = Channel.CreateUnbounded<ControlMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        _outbound[name] = channel;
        return channel.Reader;
    }

    public void Unregister(string name)
    {
        if (_outbound.TryRemove(name, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public bool TrySend(string name, ControlMessage message)
    {
        return _outbound.TryGetValue(name, out var channel)
               && channel.Writer.TryWrite(message);
    }
}