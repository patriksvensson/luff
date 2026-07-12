namespace Luff.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string Name { get; set; } = "agent";
    public string ServerAddress { get; set; } = "http://localhost:8081";
    public string ServerPin { get; set; } = string.Empty;
    public string EnrollmentSecret { get; set; } = string.Empty;
    public string CaddyAdminAddress { get; set; } = "http://localhost:2019";
    public bool HostsFrontDoor { get; set; }
}