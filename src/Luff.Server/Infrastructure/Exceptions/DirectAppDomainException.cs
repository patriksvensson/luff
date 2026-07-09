namespace Luff.Server.Infrastructure;

public sealed class DirectAppDomainException : LuffException
{
    public override string Title => "Domain not allowed";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public DirectAppDomainException()
        : base("A direct app is reached on a published port and cannot have a domain")
    {
    }
}
