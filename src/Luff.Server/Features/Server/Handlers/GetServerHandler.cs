namespace Luff.Server.Features;

public sealed class GetServerHandler : IRequestHandler<GetServerHandler.Request, ServerResponse>
{
    private readonly LuffDbContext _database;
    private readonly FrontDoorOptions _options;
    private readonly AgentLinkCertificate _certificate;

    public sealed class Request : IRequest<ServerResponse>;

    public GetServerHandler(LuffDbContext database, IOptions<FrontDoorOptions> options, AgentLinkCertificate certificate)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
    }

    public async Task<ServerResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var settings = await _database.ServerSettings.FirstOrDefaultAsync(cancellationToken);
        var domain = settings?.FrontDoorDomain ?? _options.Domain;
        return new ServerResponse(domain, settings?.AgentLinkAddress, _certificate.Pin);
    }
}

public static class GetServerHandlerExtensions
{
    public static async Task<ServerResponse> GetServer(
        this ISender sender, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new GetServerHandler.Request(), cancellationToken);
    }
}
