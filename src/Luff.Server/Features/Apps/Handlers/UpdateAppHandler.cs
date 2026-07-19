namespace Luff.Server.Features;

public sealed class UpdateAppHandler : IRequestHandler<UpdateAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly ISecretProtector _protector;
    private readonly IBasicAuthHasher _basicAuthHasher;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }
        public string Image { get; }
        public int InternalPort { get; }
        public string Actor { get; }
        public string? Domain { get; }
        public TlsMode? TlsMode { get; }

        public Request(
            string name, string image, int internalPort, string actor,
            string? domain = null, TlsMode? tlsMode = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Image = image ?? throw new ArgumentNullException(nameof(image));
            InternalPort = internalPort;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Domain = domain;
            TlsMode = tlsMode;
        }
    }

    public UpdateAppHandler(
        LuffDbContext database, IAgentConnections connections,
        ISecretProtector protector, IBasicAuthHasher basicAuthHasher, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _basicAuthHasher = basicAuthHasher ?? throw new ArgumentNullException(nameof(basicAuthHasher));
        _events = events ?? throw new ArgumentNullException(nameof(events));
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
            await PublishUpdatedAsync(app, request.Actor, cancellationToken);
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

        await PublishUpdatedAsync(app, request.Actor, cancellationToken);
        return app.ToResponse();
    }

    private Task PublishUpdatedAsync(App app, string actor, CancellationToken cancellationToken)
    {
        return _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AppUpdated,
            Actor = actor,
            Title = $"App updated: {app.Name}",
            Message = $"{app.Name} settings were updated.",
            App = app.Name,
        }, cancellationToken);
    }

    private async Task RerouteAsync(
        App app, string previousDomain, TlsRoute route, CancellationToken cancellationToken)
    {
        var agents = await _database.AppAgents
            .Where(attachment => attachment.AppName == app.Name)
            .Select(attachment => attachment.AgentName)
            .ToListAsync(cancellationToken);

        var (basicAuthUsername, basicAuthHash) = BasicAuthWire.Resolve(app, _basicAuthHasher, _protector);
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
                    BasicAuthUsername = basicAuthUsername,
                    BasicAuthHash = basicAuthHash,
                },
            });
        }
    }
}

public static class UpdateAppHandlerExtensions
{
    public static async Task<AppResponse> UpdateApp(
        this ISender sender, string name, string image, int internalPort, string actor,
        string? domain = null, TlsMode? tlsMode = null, CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new UpdateAppHandler.Request(name, image, internalPort, actor, domain, tlsMode),
            cancellationToken);
    }
}
