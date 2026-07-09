namespace Luff.Server.Infrastructure;

public sealed class InvalidPortException : LuffException
{
    public override string Title => "Invalid port";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidPortException(string reason)
        : base(reason)
    {
    }
}
