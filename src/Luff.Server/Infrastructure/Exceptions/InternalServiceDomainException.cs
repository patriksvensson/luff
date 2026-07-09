namespace Luff.Server.Infrastructure;

public sealed class InternalServiceDomainException : LuffException
{
    public override string Title => "Domain not allowed";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InternalServiceDomainException()
        : base("An internal service is not exposed and cannot have a domain")
    {
    }
}
