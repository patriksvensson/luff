namespace Luff.Server.Infrastructure;

public sealed class InvalidWebhookTokenException : LuffException
{
    public override string Title => "Invalid webhook token";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public InvalidWebhookTokenException()
        : base("The webhook token is not valid")
    {
    }
}
