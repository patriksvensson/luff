namespace Luff.Server.Features;

public sealed class AgentEnrollmentOptions
{
    public const string SectionName = "Enrollment";

    public string? Secret { get; init; }
}
