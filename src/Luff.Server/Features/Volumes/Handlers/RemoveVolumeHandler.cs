namespace Luff.Server.Features;

public sealed class RemoveVolumeHandler : IRequestHandler<RemoveVolumeHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string AppName { get; }
        public string Target { get; }
        public string Actor { get; }

        public Request(string appName, string target, string actor)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public RemoveVolumeHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var volume = await _database.Volumes.FindAsync([request.AppName, request.Target], cancellationToken)
            ?? throw new VolumeNotFoundException(request.Target, request.AppName);

        _database.Volumes.Remove(volume);
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.VolumeRemoved,
            Actor = request.Actor,
            Title = $"Volume removed: {request.AppName}",
            Message = $"The volume at {volume.Target} was removed from {request.AppName}.",
            App = request.AppName,
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class RemoveVolumeHandlerExtensions
{
    public static async Task RemoveVolume(
        this ISender sender, string appName, string target, string actor,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveVolumeHandler.Request(appName, target, actor), cancellationToken);
    }
}
