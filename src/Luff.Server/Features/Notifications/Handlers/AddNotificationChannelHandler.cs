namespace Luff.Server.Features;

public sealed class AddNotificationChannelHandler
    : IRequestHandler<AddNotificationChannelHandler.Request, NotificationChannelResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;
    private readonly TimeProvider _timeProvider;

    public sealed class Request : IRequest<NotificationChannelResponse>
    {
        public string Name { get; }
        public string Type { get; }
        public string Url { get; }

        public Request(string name, string type, string url)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }
    }

    public AddNotificationChannelHandler(LuffDbContext database, ISecretProtector protector, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<NotificationChannelResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var name = NotificationChannels.ValidateName(request.Name);
        var type = NotificationChannels.ParseType(request.Type);
        var url = NotificationChannels.ValidateUrl(request.Url);

        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Url = _protector.Protect(url),
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _database.NotificationChannels.Add(channel);
        await _database.SaveChangesAsync(cancellationToken);

        return channel.ToResponse(url);
    }
}

public static class AddNotificationChannelHandlerExtensions
{
    public static async Task<NotificationChannelResponse> AddNotificationChannel(
        this ISender sender, string name, string type, string url, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new AddNotificationChannelHandler.Request(name, type, url), cancellationToken);
    }
}
