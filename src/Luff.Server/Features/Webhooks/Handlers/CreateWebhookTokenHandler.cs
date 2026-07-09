namespace Luff.Server.Features;

public sealed class CreateWebhookTokenHandler : IRequestHandler<CreateWebhookTokenHandler.Request, CreateTokenResponse>
{
    private readonly LuffDbContext _database;
    private readonly TimeProvider _timeProvider;

    public sealed class Request : IRequest<CreateTokenResponse>
    {
        public string AppName { get; }
        public string? Name { get; }

        public Request(string appName, string? name)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Name = name;
        }
    }

    public CreateWebhookTokenHandler(LuffDbContext database, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CreateTokenResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new WebhookTokenNameRequiredException();
        }

        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken)
            ?? throw new AppNotFoundException(request.AppName);

        var token = WebhookToken.Generate();
        var entity = new WebhookToken
        {
            Id = Guid.NewGuid(),
            AppName = app.Name,
            Name = name,
            TokenHash = WebhookToken.Hash(token),
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _database.WebhookTokens.Add(entity);
        await _database.SaveChangesAsync(cancellationToken);

        return new CreateTokenResponse(entity.Id, name, token, entity.CreatedAt);
    }
}

public static class CreateWebhookTokenHandlerExtensions
{
    public static async Task<CreateTokenResponse> CreateWebhookToken(
        this ISender sender, string appName, string? name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new CreateWebhookTokenHandler.Request(appName, name), cancellationToken);
    }
}
