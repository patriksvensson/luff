namespace Luff.Server.Features;

public sealed class GetAppHandler : IRequestHandler<GetAppHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public GetAppHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<AppResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        return app.ToResponse();
    }
}

public static class GetAppHandlerExtensions
{
    public static async Task<AppResponse> GetApp(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new GetAppHandler.Request(name), cancellationToken);
    }
}
