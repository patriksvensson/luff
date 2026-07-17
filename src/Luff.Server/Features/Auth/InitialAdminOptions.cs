namespace Luff.Server.Features;

public sealed class InitialAdminOptions
{
    public const string SectionName = "Auth:InitialAdmin";

    public string? Password { get; init; }
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
