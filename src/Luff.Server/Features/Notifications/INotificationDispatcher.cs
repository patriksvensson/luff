namespace Luff.Server.Features;

// A formatted notification ready to POST to a channel's webhook URL.
public sealed record NotificationDelivery(string Url, string Body);

// Accepts deliveries and sends them out-of-band, so a slow or failing endpoint never blocks the caller.
public interface INotificationDispatcher
{
    void Enqueue(NotificationDelivery delivery);
}
