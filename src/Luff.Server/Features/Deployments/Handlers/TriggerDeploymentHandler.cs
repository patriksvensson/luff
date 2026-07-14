namespace Luff.Server.Features;

public sealed class TriggerDeploymentHandler : IRequestHandler<TriggerDeploymentHandler.Request, DeploymentResponse>
{
    private readonly LuffDbContext _database;
    private readonly DeployEngine _engine;

    public sealed class Request : IRequest<DeploymentResponse>
    {
        public string Name { get; }
        public string? Tag { get; }

        public Request(string name, string? tag)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tag = tag;
        }
    }

    public TriggerDeploymentHandler(LuffDbContext database, DeployEngine engine)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<DeploymentResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        var tag = request.Tag ?? app.CurrentImageTag
            ?? throw new DeploymentTagRequiredException(request.Name);

        app.Stopped = false;

        var queued = await _engine.QueueDeploymentAsync(app, tag, cancellationToken);

        return queued.ToResponse();
    }
}

public static class TriggerDeploymentHandlerExtensions
{
    public static async Task<DeploymentResponse> TriggerDeployment(
        this ISender sender, string name, string? tag, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new TriggerDeploymentHandler.Request(name, tag), cancellationToken);
    }
}
