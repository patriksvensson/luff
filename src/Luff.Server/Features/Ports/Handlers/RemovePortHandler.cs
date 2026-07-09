namespace Luff.Server.Features;

public sealed class RemovePortHandler : IRequestHandler<RemovePortHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string AppName { get; }
        public int HostPort { get; }

        public Request(string appName, int hostPort)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            HostPort = hostPort;
        }
    }

    public RemovePortHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var mapping = await _database.PortMappings.FindAsync([request.AppName, request.HostPort], cancellationToken)
            ?? throw new PortMappingNotFoundException(request.HostPort, request.AppName);

        _database.PortMappings.Remove(mapping);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class RemovePortHandlerExtensions
{
    public static async Task RemovePort(
        this ISender sender, string appName, int hostPort, CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemovePortHandler.Request(appName, hostPort), cancellationToken);
    }
}
