namespace Luff.Server.Features;

public sealed class RollbackHandler : IRequestHandler<RollbackHandler.Request, DeploymentResponse>
{
    private readonly LuffDbContext _database;
    private readonly DeployEngine _engine;

    public sealed class Request : IRequest<DeploymentResponse>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public RollbackHandler(LuffDbContext database, DeployEngine engine)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<DeploymentResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        var tag = app.PreviousImageTag
            ?? throw new NoPreviousDeploymentException(request.Name);

        var queued = await _engine.QueueDeploymentAsync(app, tag, cancellationToken);

        return queued.ToResponse();
    }
}

public static class RollbackHandlerExtensions
{
    public static async Task<DeploymentResponse> Rollback(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new RollbackHandler.Request(name), cancellationToken);
    }
}
