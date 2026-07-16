namespace Luff.Server.Features;

public sealed class RefreshToken : Entity
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required Guid FamilyId { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public static string Generate()
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
