namespace Luff.Server.Infrastructure;

public sealed class RegistryNotFoundException : LuffException
{
    public override string Title => "Registry not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public RegistryNotFoundException(string host)
        : base($"No registry is configured for host '{host}'")
    {
    }
}
