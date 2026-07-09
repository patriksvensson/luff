namespace Luff.Server.Infrastructure;

public sealed class PortMappingNotFoundException : LuffException
{
    public override string Title => "Port not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public PortMappingNotFoundException(int hostPort, string app)
        : base($"No published host port {hostPort} exists for app '{app}'")
    {
    }
}
