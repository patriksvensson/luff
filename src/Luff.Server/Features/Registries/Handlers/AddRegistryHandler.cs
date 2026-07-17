namespace Luff.Server.Features;

public sealed class AddRegistryHandler : IRequestHandler<AddRegistryHandler.Request, RegistryResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<RegistryResponse>
    {
        public string Host { get; }
        public string Username { get; }
        public string Password { get; }
        public string Actor { get; }

        public Request(string host, string username, string password, string actor)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public AddRegistryHandler(LuffDbContext database, ISecretProtector protector, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<RegistryResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var protectedPassword = _protector.Protect(request.Password);

        var existing = await _database.Registries.FindAsync([request.Host], cancellationToken);
        if (existing is null)
        {
            existing = new Registry
            {
                Host = request.Host,
                Username = request.Username,
                Password = protectedPassword,
            };

            _database.Registries.Add(existing);
        }
        else
        {
            existing.Username = request.Username;
            existing.Password = protectedPassword;
        }

        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.RegistryAdded,
            Actor = request.Actor,
            Title = $"Registry added: {existing.Host}",
            Message = $"Registry '{existing.Host}' credentials were saved.",
        }, cancellationToken);

        return existing.ToResponse(request.Password);
    }
}

public static class AddRegistryHandlerExtensions
{
    public static async Task<RegistryResponse> AddRegistry(
        this ISender sender, string host, string username, string password, string actor,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new AddRegistryHandler.Request(host, username, password, actor),
            cancellationToken);
    }
}
