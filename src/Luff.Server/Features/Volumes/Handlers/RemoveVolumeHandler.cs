namespace Luff.Server.Features;

public sealed class RemoveVolumeHandler : IRequestHandler<RemoveVolumeHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string AppName { get; }
        public string Target { get; }

        public Request(string appName, string target)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }
    }

    public RemoveVolumeHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var volume = await _database.Volumes.FindAsync([request.AppName, request.Target], cancellationToken)
            ?? throw new VolumeNotFoundException(request.Target, request.AppName);

        _database.Volumes.Remove(volume);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class RemoveVolumeHandlerExtensions
{
    public static async Task RemoveVolume(
        this ISender sender, string appName, string target, CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveVolumeHandler.Request(appName, target), cancellationToken);
    }
}
