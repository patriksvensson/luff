namespace Luff.Server.Infrastructure;

public sealed class WebhookTokenNotFoundException : LuffException
{
    public override string Title => "Webhook token not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public WebhookTokenNotFoundException(Guid id, string app)
        : base($"No webhook token '{id}' exists for app '{app}'")
    {
    }
}
