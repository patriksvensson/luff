using Luff.Server.Features;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeAlertPublisher : IAlertPublisher
{
    public List<Alert> Published { get; } = [];

    public Task PublishAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        Published.Add(alert);
        return Task.CompletedTask;
    }
}
