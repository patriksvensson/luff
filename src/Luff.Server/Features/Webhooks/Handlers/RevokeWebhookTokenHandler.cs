namespace Luff.Server.Features;

public sealed class RevokeWebhookTokenHandler : IRequestHandler<RevokeWebhookTokenHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string AppName { get; }
        public Guid Id { get; }

        public Request(string appName, Guid id)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Id = id;
        }
    }

    public RevokeWebhookTokenHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var token = await _database.WebhookTokens.FirstOrDefaultAsync(
            token => token.AppName == request.AppName && token.Id == request.Id,
            cancellationToken)
            ?? throw new WebhookTokenNotFoundException(request.Id, request.AppName);

        _database.WebhookTokens.Remove(token);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class RevokeWebhookTokenHandlerExtensions
{
    public static async Task<Unit> RevokeWebhookToken(
        this ISender sender, string appName, Guid id, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new RevokeWebhookTokenHandler.Request(appName, id), cancellationToken);
    }
}
