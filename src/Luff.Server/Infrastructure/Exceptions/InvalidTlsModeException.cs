namespace Luff.Server.Infrastructure;

public sealed class InvalidTlsModeException : LuffException
{
    public override string Title => "Invalid TLS mode";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidTlsModeException(string value)
        : base($"Unknown TLS mode '{value}'. Use managed or external")
    {
    }
}
