namespace Luff.Server.Features;

public sealed class NotificationChannel
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required NotificationChannelType Type { get; set; }
    public required string Url { get; set; }
    public bool Enabled { get; set; } = true;
    public required DateTimeOffset CreatedAt { get; init; }

    public NotificationChannelResponse ToResponse(string url)
    {
        return new NotificationChannelResponse(Id, Name, Type.ToString(), Enabled, CreatedAt, url);
    }
}
