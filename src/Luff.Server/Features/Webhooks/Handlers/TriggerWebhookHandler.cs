namespace Luff.Server.Features;

public sealed class TriggerWebhookHandler : IRequestHandler<TriggerWebhookHandler.Request, DeploymentResponse>
{
    private readonly LuffDbContext _database;
    private readonly TimeProvider _timeProvider;
    private readonly DeployEngine _engine;

    public sealed class Request : IRequest<DeploymentResponse>
    {
        public string Token { get; }
        public string? Tag { get; }

        public Request(string token, string? tag)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
            Tag = tag;
        }
    }

    public TriggerWebhookHandler(
        LuffDbContext database,
        TimeProvider timeProvider,
        DeployEngine engine)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<DeploymentResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var hash = WebhookToken.Hash(request.Token);
        var token = await _database.WebhookTokens.FirstOrDefaultAsync(
            token => token.TokenHash == hash, cancellationToken)
            ?? throw new InvalidWebhookTokenException();

        if (string.IsNullOrWhiteSpace(request.Tag))
        {
            throw new WebhookTagRequiredException();
        }

        token.LastUsedAt = _timeProvider.GetUtcNow();
        await _database.SaveChangesAsync(cancellationToken);

        var app = await _database.Apps.FindAsync([token.AppName], cancellationToken)
            ?? throw new AppNotFoundException(token.AppName);

        var queued = await _engine.QueueDeploymentAsync(app, request.Tag, cancellationToken);

        return queued.ToResponse();
    }
}

public static class TriggerWebhookHandlerExtensions
{
    public static async Task<DeploymentResponse> TriggerWebhook(
        this ISender sender, string token, string? tag, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new TriggerWebhookHandler.Request(token, tag), cancellationToken);
    }
}
