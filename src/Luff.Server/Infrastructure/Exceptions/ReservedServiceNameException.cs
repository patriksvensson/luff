namespace Luff.Server.Infrastructure;

public sealed class ReservedServiceNameException : LuffException
{
    public override string Title => "Reserved name";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public ReservedServiceNameException(string name)
        : base($"'{name}' is a reserved name and cannot be used for an internal service")
    {
    }
}
