namespace Luff.Server.Features;

public sealed class ListDeploymentsHandler
    : IRequestHandler<ListDeploymentsHandler.Request, IReadOnlyList<DeploymentResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<DeploymentResponse>>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public ListDeploymentsHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<DeploymentResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var exists = await _database.Apps.AnyAsync(app => app.Name == request.Name, cancellationToken);
        if (!exists)
        {
            throw new AppNotFoundException(request.Name);
        }

        var deployments = await _database.Deployments
            .Where(deployment => deployment.AppName == request.Name)
            .ToListAsync(cancellationToken);

        return
        [
            .. deployments
                .OrderByDescending(deployment => deployment.CreatedAt)
                .Select(deployment => deployment.ToResponse()),
        ];
    }
}

public static class ListDeploymentsHandlerExtensions
{
    public static async Task<IReadOnlyList<DeploymentResponse>> ListDeployments(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListDeploymentsHandler.Request(name), cancellationToken);
    }
}
