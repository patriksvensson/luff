namespace Luff.Server.Features;

public sealed class AddPortRequest
{
    public int HostPort { get; }
    public int ContainerPort { get; }

    public AddPortRequest(int hostPort, int containerPort)
    {
        HostPort = hostPort;
        ContainerPort = containerPort;
    }
}

public sealed class PortMappingResponse
{
    public int HostPort { get; }
    public int ContainerPort { get; }

    public PortMappingResponse(int hostPort, int containerPort)
    {
        HostPort = hostPort;
        ContainerPort = containerPort;
    }
}
