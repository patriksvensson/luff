namespace Luff.Server.Features;

public interface IFleetEvents
{
    void Publish(string agent, bool connected);

    IDisposable Subscribe(Func<string, bool, Task> handler);
}

public sealed class FleetEvents : IFleetEvents
{
    private readonly object _gate = new();
    private readonly List<Subscription> _subscribers = [];

    public void Publish(string agent, bool connected)
    {
        List<Channel<(string, bool)>> targets;
        lock (_gate)
        {
            targets = [.. _subscribers.Select(subscription => subscription.Channel)];
        }

        foreach (var target in targets)
        {
            target.Writer.TryWrite((agent, connected));
        }
    }

    public IDisposable Subscribe(Func<string, bool, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var channel = Channel.CreateUnbounded<(string, bool)>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        var subscription = new Subscription(this, channel);
        lock (_gate)
        {
            _subscribers.Add(subscription);
        }

        subscription.Start(handler);
        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscription);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly FleetEvents _owner;
        private readonly CancellationTokenSource _cts = new();

        public Channel<(string Agent, bool Connected)> Channel { get; }

        public Subscription(FleetEvents owner, Channel<(string, bool)> channel)
        {
            _owner = owner;
            Channel = channel;
        }

        public void Start(Func<string, bool, Task> handler)
        {
            _ = ReadLoop(handler);
        }

        public void Dispose()
        {
            _cts.Cancel();
            Channel.Writer.TryComplete();
            _owner.Remove(this);
        }

        private async Task ReadLoop(Func<string, bool, Task> handler)
        {
            try
            {
                await foreach (var (agent, connected) in Channel.Reader.ReadAllAsync(_cts.Token))
                {
                    await handler(agent, connected);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
