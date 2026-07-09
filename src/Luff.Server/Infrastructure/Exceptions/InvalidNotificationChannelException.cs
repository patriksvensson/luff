namespace Luff.Server.Infrastructure;

public sealed class InvalidNotificationChannelException : LuffException
{
    public override string Title => "Invalid notification channel";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidNotificationChannelException(string message)
        : base(message)
    {
    }
}
