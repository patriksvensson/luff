namespace Luff.Server.Features;

public sealed class TriggerWebhookRequest
{
    public string? Tag { get; init; }
}

public sealed class CreateTokenRequest
{
    public string? Name { get; init; }
}

public sealed class CreateTokenResponse
{
    public Guid Id { get; }
    public string Name { get; }
    public string Token { get; }
    public DateTimeOffset CreatedAt { get; }

    public CreateTokenResponse(Guid id, string name, string token, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Token = token ?? throw new ArgumentNullException(nameof(token));
        CreatedAt = createdAt;
    }
}

public sealed class TokenResponse
{
    public Guid Id { get; }
    public string? Name { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? LastUsedAt { get; }

    public TokenResponse(Guid id, string? name, DateTimeOffset createdAt, DateTimeOffset? lastUsedAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        LastUsedAt = lastUsedAt;
    }
}
