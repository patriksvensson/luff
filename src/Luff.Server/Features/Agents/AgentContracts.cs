namespace Luff.Server.Features;

public sealed class AgentResponse
{
    public string Name { get; }
    public AgentConnectionStatus Status { get; }
    public string Version { get; }

    public AgentResponse(string name, AgentConnectionStatus status, string version)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Status = status;
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }
}

public sealed class EnrollAgentRequest
{
    public string Name { get; }

    public EnrollAgentRequest(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

public sealed class EnrollAgentResponse
{
    public string Name { get; }
    public string Token { get; }

    public EnrollAgentResponse(string name, string token)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Token = token ?? throw new ArgumentNullException(nameof(token));
    }
}
