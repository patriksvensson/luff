namespace Luff.Server.Features;

public sealed class ServerSettings : Entity
{
    public const string SingletonId = "singleton";

    public string Id { get; init; } = SingletonId;
    public required string FrontDoorDomain { get; set; }
    public string? AgentLinkAddress { get; set; }
}
