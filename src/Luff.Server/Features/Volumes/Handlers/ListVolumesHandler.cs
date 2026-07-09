namespace Luff.Server.Features;

public sealed class ListVolumesHandler : IRequestHandler<ListVolumesHandler.Request, IReadOnlyList<VolumeResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<VolumeResponse>>
    {
        public string AppName { get; }

        public Request(string appName)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        }
    }

    public ListVolumesHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<VolumeResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var exists = await _database.Apps.AnyAsync(app => app.Name == request.AppName, cancellationToken);
        if (!exists)
        {
            throw new AppNotFoundException(request.AppName);
        }

        var volumes = await _database.Volumes
            .Where(volume => volume.AppName == request.AppName)
            .OrderBy(volume => volume.Target)
            .ToListAsync(cancellationToken);

        return [.. volumes.Select(volume => volume.ToResponse())];
    }
}

public static class ListVolumesHandlerExtensions
{
    public static async Task<IReadOnlyList<VolumeResponse>> ListVolumes(
        this ISender sender, string appName, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListVolumesHandler.Request(appName), cancellationToken);
    }
}
