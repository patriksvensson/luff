namespace Luff.Server.Infrastructure;

public sealed class InvalidHealthCheckException : LuffException
{
    public override string Title => "Invalid health check";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidHealthCheckException(string reason)
        : base(reason)
    {
    }
}
