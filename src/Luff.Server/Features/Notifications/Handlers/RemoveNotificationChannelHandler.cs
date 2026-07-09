namespace Luff.Server.Features;

public sealed class RemoveNotificationChannelHandler : IRequestHandler<RemoveNotificationChannelHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public Guid Id { get; }

        public Request(Guid id)
        {
            Id = id;
        }
    }

    public RemoveNotificationChannelHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var channel = await _database.NotificationChannels.FindAsync([request.Id], cancellationToken)
            ?? throw new NotificationChannelNotFoundException(request.Id);

        _database.NotificationChannels.Remove(channel);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class RemoveNotificationChannelHandlerExtensions
{
    public static async Task RemoveNotificationChannel(
        this ISender sender, Guid id, CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveNotificationChannelHandler.Request(id), cancellationToken);
    }
}
