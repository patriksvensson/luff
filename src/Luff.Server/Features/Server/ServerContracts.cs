namespace Luff.Server.Features;

public sealed class ServerResponse
{
    public string FrontDoorDomain { get; }
    public string? AgentLinkAddress { get; }
    public string AgentLinkPin { get; }
    public string Version => ServerVersion.Current;

    public ServerResponse(string frontDoorDomain, string? agentLinkAddress, string agentLinkPin)
    {
        FrontDoorDomain = frontDoorDomain ?? throw new ArgumentNullException(nameof(frontDoorDomain));
        AgentLinkAddress = agentLinkAddress;
        AgentLinkPin = agentLinkPin ?? throw new ArgumentNullException(nameof(agentLinkPin));
    }
}

public sealed class SetDomainRequest
{
    public string Domain { get; }

    public SetDomainRequest(string domain)
    {
        Domain = domain ?? throw new ArgumentNullException(nameof(domain));
    }
}

public sealed class SetAgentLinkRequest
{
    public string Address { get; }

    public SetAgentLinkRequest(string address)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
    }
}
