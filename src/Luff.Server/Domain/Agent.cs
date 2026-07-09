namespace Luff.Server.Features;

public sealed class Agent
{
    public required string Name { get; init; }
    public string? EnrollmentTokenHash { get; set; }
    public required DateTimeOffset RegisteredAt { get; init; }
    public DateTimeOffset? LastSeenAt { get; set; }

    public static string GenerateToken()
    {
        return $"luff_{RandomNumberGenerator.GetHexString(64, lowercase: true)}";
    }

    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }
}
