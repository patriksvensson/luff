namespace Luff.Server.Features;

public sealed class AddRegistryHandler : IRequestHandler<AddRegistryHandler.Request, RegistryResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<RegistryResponse>
    {
        public string Host { get; }
        public string Username { get; }
        public string Password { get; }

        public Request(string host, string username, string password)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
        }
    }

    public AddRegistryHandler(LuffDbContext database, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
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

        return existing.ToResponse(request.Password);
    }
}

public static class AddRegistryHandlerExtensions
{
    public static async Task<RegistryResponse> AddRegistry(
        this ISender sender, string host, string username, string password,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new AddRegistryHandler.Request(host, username, password),
            cancellationToken);
    }
}
