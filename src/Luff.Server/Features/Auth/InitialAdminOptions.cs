namespace Luff.Server.Features;

public sealed class InitialAdminOptions
{
    public const string SectionName = "Auth:InitialAdmin";

    public string? Username { get; init; }
    public string? Password { get; init; }
}
