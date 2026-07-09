namespace Luff.Server.Features;

public sealed class ListAppsHandler : IRequestHandler<ListAppsHandler.Request, IReadOnlyList<AppResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<AppResponse>>
    {
    }

    public ListAppsHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<AppResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var apps = await _database.Apps
            .OrderBy(app => app.Name)
            .ToListAsync(cancellationToken);

        return [.. apps.Select(app => app.ToResponse())];
    }
}

public static class ListAppsHandlerExtensions
{
    public static async Task<IReadOnlyList<AppResponse>> ListApps(
        this ISender sender, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListAppsHandler.Request(), cancellationToken);
    }
}
