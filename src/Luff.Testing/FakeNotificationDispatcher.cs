using Luff.Server.Features;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeNotificationDispatcher : INotificationDispatcher
{
    public List<NotificationDelivery> Deliveries { get; } = [];

    public void Enqueue(NotificationDelivery delivery)
    {
        Deliveries.Add(delivery);
    }
}
