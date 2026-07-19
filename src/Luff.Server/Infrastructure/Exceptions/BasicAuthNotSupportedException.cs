namespace Luff.Server.Infrastructure;

public sealed class BasicAuthNotSupportedException : LuffException
{
    public override string Title => "Basic authentication not available";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public BasicAuthNotSupportedException()
        : base("Basic authentication is only available for web apps")
    {
    }
}
