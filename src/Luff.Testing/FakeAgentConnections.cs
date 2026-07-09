using System.Collections.Concurrent;
using System.Threading.Channels;
using Luff.Protobuf;
using Luff.Server.Features;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeAgentConnections : IAgentConnections
{
    private readonly ConcurrentDictionary<string, Channel<ControlMessage>> _outbound = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Connected => [.. _outbound.Keys];

    public void ClearAgents()
    {
        _outbound.Clear();
    }

    public ChannelReader<ControlMessage> GetChannel(string name)
    {
        return _outbound[name].Reader;
    }

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