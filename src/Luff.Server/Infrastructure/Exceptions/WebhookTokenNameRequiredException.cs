namespace Luff.Server.Infrastructure;

public sealed class WebhookTokenNameRequiredException : LuffException
{
    public override string Title => "Name required";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public WebhookTokenNameRequiredException()
        : base("A webhook token requires a name")
    {
    }
}
