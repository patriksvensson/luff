namespace Luff.Server.Features;

public sealed class SetBasicAuthHandler : IRequestHandler<SetBasicAuthHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly ISecretProtector _protector;
    private readonly IBasicAuthHasher _basicAuthHasher;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string Name { get; }
        public string Username { get; }
        public string Password { get; }
        public string Actor { get; }

        public Request(string name, string username, string password, string actor)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public SetBasicAuthHandler(
        LuffDbContext database, IAgentConnections connections,
        ISecretProtector protector, IBasicAuthHasher basicAuthHasher, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _basicAuthHasher = basicAuthHasher ?? throw new ArgumentNullException(nameof(basicAuthHasher));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        if (!app.IsCaddyFronted)
        {
            throw new BasicAuthNotSupportedException();
        }

        var username = request.Username.Trim();
        if (username.Length == 0 || request.Password.Length == 0)
        {
            throw new InvalidBasicAuthException("A basic-auth username and password are required");
        }

        if (username.Contains(':'))
        {
            // The client sends `user:pass`, so a colon in the username yields a gate that can never be satisfied.
            throw new InvalidBasicAuthException("A basic-auth username cannot contain a colon");
        }

        app.BasicAuthUsername = username;
        app.BasicAuthPassword = _protector.Protect(request.Password);
        await _database.SaveChangesAsync(cancellationToken);

        await BasicAuthRouting.ReassertAsync(_database, _connections, _basicAuthHasher, _protector, app, cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AppUpdated,
            Actor = request.Actor,
            Title = $"App updated: {app.Name}",
            Message = $"Basic authentication was enabled for {app.Name}.",
            App = app.Name,
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class SetBasicAuthHandlerExtensions
{
    public static async Task SetBasicAuth(
        this ISender sender, string name, string username, string password, string actor,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new SetBasicAuthHandler.Request(name, username, password, actor), cancellationToken);
    }
}
