namespace Luff.Server.Features;

public sealed class WebhookToken
{
    public required Guid Id { get; init; }
    public required string AppName { get; init; }
    public string? Name { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public TokenResponse ToResponse()
    {
        return new TokenResponse(Id, Name, CreatedAt, LastUsedAt);
    }

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
