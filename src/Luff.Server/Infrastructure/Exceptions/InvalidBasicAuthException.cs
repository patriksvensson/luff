namespace Luff.Server.Infrastructure;

public sealed class InvalidBasicAuthException : LuffException
{
    public override string Title => "Invalid basic auth credential";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidBasicAuthException(string message)
        : base(message)
    {
    }
}
