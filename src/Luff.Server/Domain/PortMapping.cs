namespace Luff.Server.Features;

public sealed class PortMapping
{
    public required string AppName { get; init; }
    public required int HostPort { get; init; }
    public required int ContainerPort { get; set; }

    public PortMappingResponse ToResponse()
    {
        return new PortMappingResponse(HostPort, ContainerPort);
    }
}
