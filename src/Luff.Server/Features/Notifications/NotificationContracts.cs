namespace Luff.Server.Features;

public sealed class CreateNotificationChannelRequest
{
    public string Name { get; }
    public string Type { get; }
    public string Url { get; }

    public CreateNotificationChannelRequest(string name, string type, string url)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Url = url ?? throw new ArgumentNullException(nameof(url));
    }
}

public sealed class NotificationChannelResponse
{
    public Guid Id { get; }
    public string Name { get; }
    public string Type { get; }
    public bool Enabled { get; }
    public DateTimeOffset CreatedAt { get; }
    public string Url { get; }

    public NotificationChannelResponse(
        Guid id, string name, string type, bool enabled, DateTimeOffset createdAt, string url)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Enabled = enabled;
        CreatedAt = createdAt;
        Url = url ?? throw new ArgumentNullException(nameof(url));
    }
}
