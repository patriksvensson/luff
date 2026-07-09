namespace Luff.Server.Infrastructure;

public sealed class WebhookTagRequiredException : LuffException
{
    public override string Title => "No tag to deploy";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public WebhookTagRequiredException()
        : base("The webhook request did not include an image tag")
    {
    }
}
