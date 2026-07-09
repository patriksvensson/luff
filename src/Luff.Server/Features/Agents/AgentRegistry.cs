namespace Luff.Server.Features;

public sealed class AgentRegistry
{
    private readonly ConcurrentDictionary<string, AgentSnapshot> _agents = new(StringComparer.Ordinal);

    public IReadOnlyCollection<AgentSnapshot> List()
    {
        return [.. _agents.Values];
    }

    public bool Knows(string name)
    {
        return _agents.ContainsKey(name);
    }

    public bool IsFrontDoorHost(string name)
    {
        return _agents.TryGetValue(name, out var agent) && agent.HostsFrontDoor;
    }

    public void MarkConnected(string name, string version, bool hostsFrontDoor = false)
    {
        _agents[name] = new AgentSnapshot(name, AgentConnectionStatus.Connected, version, hostsFrontDoor);
    }

    public void MarkDisconnected(string name)
    {
        if (_agents.TryGetValue(name, out var existing))
        {
            _agents[name] = new AgentSnapshot(
                existing.Name, AgentConnectionStatus.Disconnected, existing.Version, existing.HostsFrontDoor);
        }
    }
}
