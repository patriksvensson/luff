namespace Luff.Server.Features;

public sealed class UpdateAppHandler : IRequestHandler<UpdateAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }
        public string Image { get; }
        public int InternalPort { get; }
        public string? Domain { get; }
        public TlsMode? TlsMode { get; }

        public Request(string name, string image, int internalPort, string? domain = null, TlsMode? tlsMode = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Image = image ?? throw new ArgumentNullException(nameof(image));
            InternalPort = internalPort;
            Domain = domain;
            TlsMode = tlsMode;
        }
    }

    public UpdateAppHandler(LuffDbContext database, IAgentConnections connections)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public async Task<AppResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        var domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim();

        // A frontless app (internal or direct) has no domain or route.
        // Update image/port only, never reroute.
        if (!app.IsCaddyFronted)
        {
            if (domain is not null)
            {
                if (app.Kind == AppKind.Internal)
                {
                    throw new InternalServiceDomainException();
                }

                throw new DirectAppDomainException();
            }

            app.Image = request.Image;
            app.InternalPort = request.InternalPort;
            await _database.SaveChangesAsync(cancellationToken);
            return app.ToResponse();
        }

        if (domain is null)
        {
            throw new InvalidDomainException();
        }

        var previousDomain = app.Domain!;
        var previousRoute = TlsRouting.Resolve(app);

        app.Image = request.Image;
        app.Domain = domain;
        app.InternalPort = request.InternalPort;
        app.TlsMode = request.TlsMode ?? TlsMode.Managed;

        await _database.SaveChangesAsync(cancellationToken);

        var route = TlsRouting.Resolve(app);
        if (!string.Equals(previousDomain, app.Domain, StringComparison.Ordinal) || previousRoute != route)
        {
            await RerouteAsync(app, previousDomain, route, cancellationToken);
        }

        return app.ToResponse();
    }

    private async Task RerouteAsync(
        App app, string previousDomain, TlsRoute route, CancellationToken cancellationToken)
    {
        var agents = await _database.AppAgents
            .Where(attachment => attachment.AppName == app.Name)
            .Select(attachment => attachment.AgentName)
            .ToListAsync(cancellationToken);

        foreach (var agent in agents)
        {
            _connections.TrySend(agent, new ControlMessage
            {
                Reroute = new Reroute
                {
                    App = app.Name,
                    OldDomain = previousDomain,
                    NewDomain = app.Domain,
                    Route = route,
                },
            });
        }
    }
}

public static class UpdateAppHandlerExtensions
{
    public static async Task<AppResponse> UpdateApp(
        this ISender sender, string name, string image, int internalPort,
        string? domain = null, TlsMode? tlsMode = null, CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new UpdateAppHandler.Request(name, image, internalPort, domain, tlsMode),
            cancellationToken);
    }
}
