namespace Luff.Server.Infrastructure;

public sealed class InvalidAppKindException : LuffException
{
    public override string Title => "Invalid app kind";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidAppKindException(string value)
        : base($"'{value}' is not a valid app kind")
    {
    }
}
