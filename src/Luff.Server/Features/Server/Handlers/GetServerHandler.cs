namespace Luff.Server.Features;

public sealed class GetServerHandler : IRequestHandler<GetServerHandler.Request, ServerResponse>
{
    private readonly LuffDbContext _database;
    private readonly FrontDoorOptions _options;

    public sealed class Request : IRequest<ServerResponse>;

    public GetServerHandler(LuffDbContext database, IOptions<FrontDoorOptions> options)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ServerResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var domain = await FrontDoor.ResolveDomainAsync(_database, _options, cancellationToken);
        return new ServerResponse(domain);
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
