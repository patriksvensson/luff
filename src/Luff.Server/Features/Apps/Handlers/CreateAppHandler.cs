namespace Luff.Server.Features;

public sealed class CreateAppHandler : IRequestHandler<CreateAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }
        public string Image { get; }
        public int InternalPort { get; }
        public string Actor { get; }
        public AppKind? Kind { get; }
        public string? Domain { get; }
        public TlsMode? TlsMode { get; }

        public Request(
            string name, string image, int internalPort, string actor,
            AppKind? kind = null, string? domain = null, TlsMode? tlsMode = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Image = image ?? throw new ArgumentNullException(nameof(image));
            InternalPort = internalPort;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Kind = kind;
            Domain = domain;
            TlsMode = tlsMode;
        }
    }

    public CreateAppHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<AppResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var exists = await _database.Apps.AnyAsync(app => app.Name == request.Name, cancellationToken);
        if (exists)
        {
            throw new AppAlreadyExistsException(request.Name);
        }

        var kind = request.Kind ?? AppKind.Web;
        var domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim();

        if (kind == AppKind.Web && domain is null)
        {
            throw new InvalidDomainException();
        }

        if (kind != AppKind.Web)
        {
            if (domain is not null)
            {
                if (kind == AppKind.Internal)
                {
                    throw new InternalServiceDomainException();
                }

                throw new DirectAppDomainException();
            }

            if (AppKinds.IsReservedName(request.Name))
            {
                throw new ReservedServiceNameException(request.Name);
            }
        }

        var app = new App
        {
            Name = request.Name,
            Kind = kind,
            Image = request.Image,
            Domain = domain,
            InternalPort = request.InternalPort,
            // TLS is only meaningful for a web app's route; an internal service keeps the default and ignores it.
            TlsMode = request.TlsMode ?? TlsMode.Managed,
            // An internal service has no HTTP endpoint, so default it to the agent-side TCP readiness probe.
            HealthCheckType = kind == AppKind.Web ? AppHealthCheckType.Docker : AppHealthCheckType.Tcp,
        };

        _database.Apps.Add(app);
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AppCreated,
            Actor = request.Actor,
            Title = $"App created: {app.Name}",
            Message = $"{app.Name} ({app.Kind}) was created on image {app.Image}.",
            App = app.Name,
        }, cancellationToken);

        return app.ToResponse();
    }
}

public static class CreateAppHandlerExtensions
{
    public static async Task<AppResponse> CreateApp(
        this ISender sender, string name, string image, int internalPort, string actor,
        AppKind? kind = null, string? domain = null, TlsMode? tlsMode = null,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new CreateAppHandler.Request(name, image, internalPort, actor, kind, domain, tlsMode),
            cancellationToken);
    }
}
