namespace Luff.Server.Features;

public sealed class FrontDoorConfigurator
{
    private readonly IAgentConnections _connections;
    private readonly AgentRegistry _registry;
    private readonly FrontDoorOptions _options;

    public FrontDoorConfigurator(
        IAgentConnections connections, AgentRegistry registry, IOptions<FrontDoorOptions> options)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public void ConfigureAgentIfHost(string agentName, string domain)
    {
        if (_registry.IsFrontDoorHost(agentName))
        {
            Send(agentName, domain);
        }
    }

    public void ConfigureConnected(string domain)
    {
        foreach (var agent in _registry.List())
        {
            if (agent.HostsFrontDoor)
            {
                Send(agent.Name, domain);
            }
        }
    }

    private void Send(string agentName, string domain)
    {
        _connections.TrySend(agentName, new ControlMessage
        {
            ConfigureFrontDoor = new ConfigureFrontDoor
            {
                Domain = domain,
                Upstream = _options.Upstream,
            },
        });
    }
}
