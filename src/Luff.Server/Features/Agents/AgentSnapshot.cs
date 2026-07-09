namespace Luff.Server.Features;

public sealed class AgentSnapshot
{
    public string Name { get; }
    public AgentConnectionStatus Status { get; }
    public string Version { get; }
    public bool HostsFrontDoor { get; }

    public AgentSnapshot(string name, AgentConnectionStatus status, string version, bool hostsFrontDoor)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Status = status;
        Version = version ?? throw new ArgumentNullException(nameof(version));
        HostsFrontDoor = hostsFrontDoor;
    }
}
