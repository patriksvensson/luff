namespace Luff.Server.Infrastructure;

public sealed class NotificationChannelNotFoundException : LuffException
{
    public override string Title => "Notification channel not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public NotificationChannelNotFoundException(Guid id)
        : base($"No notification channel with id '{id}'")
    {
    }
}
