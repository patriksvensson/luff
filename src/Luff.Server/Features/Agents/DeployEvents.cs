namespace Luff.Server.Features;

public enum DeployEventKind
{
    Progress,
    Result,
}

public sealed record DeployEvent(
    DeployEventKind Kind,
    Guid DeploymentId,
    string Agent,
    DeployPhase? Phase,
    bool Healthy,
    string? RunningTag,
    string? FailureReason);

public interface IDeployEvents
{
    void PublishProgress(Guid deploymentId, string agent, DeployPhase phase);

    void PublishResult(Guid deploymentId, string agent, bool healthy, string runningTag, string? failureReason);

    IDisposable Subscribe(Guid deploymentId, Func<DeployEvent, Task> handler);
}

public sealed class DeployEvents : IDeployEvents
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Feed> _feeds = new();

    public void PublishProgress(Guid deploymentId, string agent, DeployPhase phase)
    {
        Publish(
            deploymentId,
            new DeployEvent(DeployEventKind.Progress, deploymentId, agent, phase, false, null, null),
            completes: false);
    }

    public void PublishResult(Guid deploymentId, string agent, bool healthy, string runningTag, string? failureReason)
    {
        Publish(
            deploymentId,
            new DeployEvent(DeployEventKind.Result, deploymentId, agent, null, healthy, runningTag, failureReason),
            completes: true);
    }

    public IDisposable Subscribe(Guid deploymentId, Func<DeployEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var channel = Channel.CreateUnbounded<DeployEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        Subscription subscription;
        lock (_gate)
        {
            var feed = GetOrAdd(deploymentId);
            foreach (var replayed in feed.History)
            {
                channel.Writer.TryWrite(replayed);
            }

            subscription = new Subscription(this, deploymentId, channel);
            feed.Subscribers.Add(subscription);
        }

        subscription.Start(handler);
        return subscription;
    }

    private void Publish(Guid deploymentId, DeployEvent deployEvent, bool completes)
    {
        List<Channel<DeployEvent>> targets;
        lock (_gate)
        {
            var feed = GetOrAdd(deploymentId);
            feed.History.Add(deployEvent);
            if (completes)
            {
                feed.Completed = true;
            }

            targets = [.. feed.Subscribers.Select(subscription => subscription.Channel)];

            if (completes && feed.Subscribers.Count == 0)
            {
                _feeds.Remove(deploymentId);
            }
        }

        foreach (var target in targets)
        {
            target.Writer.TryWrite(deployEvent);
        }
    }

    private Feed GetOrAdd(Guid deploymentId)
    {
        if (!_feeds.TryGetValue(deploymentId, out var feed))
        {
            feed = new Feed();
            _feeds[deploymentId] = feed;
        }

        return feed;
    }

    private void Remove(Guid deploymentId, Subscription subscription)
    {
        lock (_gate)
        {
            if (_feeds.TryGetValue(deploymentId, out var feed))
            {
                feed.Subscribers.Remove(subscription);
                if (feed.Completed && feed.Subscribers.Count == 0)
                {
                    _feeds.Remove(deploymentId);
                }
            }
        }
    }

    private sealed class Feed
    {
        public List<DeployEvent> History { get; } = [];
        public List<Subscription> Subscribers { get; } = [];
        public bool Completed { get; set; }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly DeployEvents _owner;
        private readonly Guid _deploymentId;
        private readonly CancellationTokenSource _cts = new();

        public Channel<DeployEvent> Channel { get; }

        public Subscription(DeployEvents owner, Guid deploymentId, Channel<DeployEvent> channel)
        {
            _owner = owner;
            _deploymentId = deploymentId;
            Channel = channel;
        }

        public void Start(Func<DeployEvent, Task> handler)
        {
            _ = ReadLoop(handler);
        }

        public void Dispose()
        {
            _cts.Cancel();
            Channel.Writer.TryComplete();
            _owner.Remove(_deploymentId, this);
        }

        private async Task ReadLoop(Func<DeployEvent, Task> handler)
        {
            try
            {
                await foreach (var deployEvent in Channel.Reader.ReadAllAsync(_cts.Token))
                {
                    await handler(deployEvent);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
