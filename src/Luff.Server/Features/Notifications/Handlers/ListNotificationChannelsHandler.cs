namespace Luff.Server.Features;

public sealed class ListNotificationChannelsHandler
    : IRequestHandler<ListNotificationChannelsHandler.Request, IReadOnlyList<NotificationChannelResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<NotificationChannelResponse>>;

    public ListNotificationChannelsHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<NotificationChannelResponse>> Handle(
        Request request, CancellationToken cancellationToken)
    {
        var channels = await _database.NotificationChannels.ToListAsync(cancellationToken);

        return
        [
            .. channels
                .OrderBy(channel => channel.CreatedAt)
                .Select(channel => channel.ToResponse()),
        ];
    }
}

public static class ListNotificationChannelsHandlerExtensions
{
    public static async Task<IReadOnlyList<NotificationChannelResponse>> ListNotificationChannels(
        this ISender sender, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListNotificationChannelsHandler.Request(), cancellationToken);
    }
}
