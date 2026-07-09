namespace Luff.Server.Infrastructure;

public sealed class InvalidDomainException : LuffException
{
    public override string Title => "Invalid domain";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidDomainException()
        : base("The domain must not be empty")
    {
    }
}
