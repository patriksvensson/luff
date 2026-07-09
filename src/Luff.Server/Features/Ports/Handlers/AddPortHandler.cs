namespace Luff.Server.Features;

public sealed class AddPortHandler : IRequestHandler<AddPortHandler.Request, PortMappingResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<PortMappingResponse>
    {
        public string AppName { get; }
        public int HostPort { get; }
        public int ContainerPort { get; }

        public Request(string appName, int hostPort, int containerPort)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            HostPort = hostPort;
            ContainerPort = containerPort;
        }
    }

    public AddPortHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<PortMappingResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken)
            ?? throw new AppNotFoundException(request.AppName);

        if (app.Kind != AppKind.Direct)
        {
            throw new InvalidPortException("Only a direct app can publish ports");
        }

        var error = PortValidator.Validate(request.HostPort, request.ContainerPort);
        if (error is not null)
        {
            throw new InvalidPortException(error);
        }

        var taken = await _database.PortMappings.AnyAsync(
            mapping => mapping.HostPort == request.HostPort && mapping.AppName != app.Name,
            cancellationToken);

        if (taken)
        {
            throw new InvalidPortException($"The host port {request.HostPort} is already published by another app");
        }

        var existing = await _database.PortMappings.FindAsync([app.Name, request.HostPort], cancellationToken);
        if (existing is null)
        {
            existing = new PortMapping
            {
                AppName = app.Name,
                HostPort = request.HostPort,
                ContainerPort = request.ContainerPort,
            };

            _database.PortMappings.Add(existing);
        }
        else
        {
            existing.ContainerPort = request.ContainerPort;
        }

        await _database.SaveChangesAsync(cancellationToken);

        return existing.ToResponse();
    }
}

public static class AddPortHandlerExtensions
{
    public static async Task<PortMappingResponse> AddPort(
        this ISender sender, string appName, int hostPort, int containerPort,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(new AddPortHandler.Request(appName, hostPort, containerPort), cancellationToken);
    }
}
