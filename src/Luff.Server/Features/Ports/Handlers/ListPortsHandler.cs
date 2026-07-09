namespace Luff.Server.Features;

public sealed class ListPortsHandler : IRequestHandler<ListPortsHandler.Request, IReadOnlyList<PortMappingResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<PortMappingResponse>>
    {
        public string AppName { get; }

        public Request(string appName)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        }
    }

    public ListPortsHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<PortMappingResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var exists = await _database.Apps.AnyAsync(app => app.Name == request.AppName, cancellationToken);
        if (!exists)
        {
            throw new AppNotFoundException(request.AppName);
        }

        var mappings = await _database.PortMappings
            .Where(mapping => mapping.AppName == request.AppName)
            .OrderBy(mapping => mapping.HostPort)
            .ToListAsync(cancellationToken);

        return [.. mappings.Select(mapping => mapping.ToResponse())];
    }
}

public static class ListPortsHandlerExtensions
{
    public static async Task<IReadOnlyList<PortMappingResponse>> ListPorts(
        this ISender sender, string appName, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListPortsHandler.Request(appName), cancellationToken);
    }
}
