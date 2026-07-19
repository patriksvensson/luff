namespace Luff.Server.Features;

public sealed class GetBasicAuthHandler : IRequestHandler<GetBasicAuthHandler.Request, BasicAuthResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<BasicAuthResponse>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public GetBasicAuthHandler(LuffDbContext database, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<BasicAuthResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        if (string.IsNullOrEmpty(app.BasicAuthUsername) || string.IsNullOrEmpty(app.BasicAuthPassword))
        {
            return new BasicAuthResponse(false, null, null);
        }

        return new BasicAuthResponse(true, app.BasicAuthUsername, _protector.Unprotect(app.BasicAuthPassword));
    }
}

public static class GetBasicAuthHandlerExtensions
{
    public static async Task<BasicAuthResponse> GetBasicAuth(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new GetBasicAuthHandler.Request(name), cancellationToken);
    }
}
