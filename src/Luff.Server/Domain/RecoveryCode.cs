namespace Luff.Server.Features;

public sealed class RecoveryCode
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string CodeHash { get; init; }
    public DateTimeOffset? ConsumedAt { get; set; }

    public static string Generate()
    {
        return RandomNumberGenerator.GetHexString(10, lowercase: true);
    }

    public static string Hash(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var normalized = code.Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty).ToLowerInvariant();

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(normalized));

        return Convert.ToHexStringLower(hash);
    }
}