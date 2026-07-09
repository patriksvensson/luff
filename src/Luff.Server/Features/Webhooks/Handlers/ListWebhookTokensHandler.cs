namespace Luff.Server.Features;

public sealed class ListWebhookTokensHandler
    : IRequestHandler<ListWebhookTokensHandler.Request, IReadOnlyList<TokenResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<TokenResponse>>
    {
        public string AppName { get; }

        public Request(string appName)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        }
    }

    public ListWebhookTokensHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<TokenResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var exists = await _database.Apps.AnyAsync(app => app.Name == request.AppName, cancellationToken);
        if (!exists)
        {
            throw new AppNotFoundException(request.AppName);
        }

        var tokens = await _database.WebhookTokens
            .Where(token => token.AppName == request.AppName)
            .ToListAsync(cancellationToken);

        return
        [
            .. tokens
                .OrderByDescending(token => token.CreatedAt)
                .Select(token => token.ToResponse()),
        ];
    }
}

public static class ListWebhookTokensHandlerExtensions
{
    public static async Task<IReadOnlyList<TokenResponse>> ListWebhookTokens(
        this ISender sender, string appName, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListWebhookTokensHandler.Request(appName), cancellationToken);
    }
}
