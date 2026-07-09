namespace Luff.Server.Features;

public sealed class ServerResponse
{
    public string FrontDoorDomain { get; }

    public ServerResponse(string frontDoorDomain)
    {
        FrontDoorDomain = frontDoorDomain ?? throw new ArgumentNullException(nameof(frontDoorDomain));
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
