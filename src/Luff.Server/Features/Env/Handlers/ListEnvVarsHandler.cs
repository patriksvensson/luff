namespace Luff.Server.Features;

public sealed class ListEnvVarsHandler : IRequestHandler<ListEnvVarsHandler.Request, IReadOnlyList<EnvVarResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<EnvVarResponse>>
    {
        public string AppName { get; }

        public Request(string appName)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        }
    }

    public ListEnvVarsHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<EnvVarResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var exists = await _database.Apps.AnyAsync(app => app.Name == request.AppName, cancellationToken);
        if (!exists)
        {
            throw new AppNotFoundException(request.AppName);
        }

        var vars = await _database.EnvVars
            .Where(env => env.AppName == request.AppName)
            .OrderBy(env => env.Key)
            .ToListAsync(cancellationToken);

        return [.. vars.Select(env => env.ToResponse())];
    }
}

public static class ListEnvVarsHandlerExtensions
{
    public static async Task<IReadOnlyList<EnvVarResponse>> ListEnvVars(
        this ISender sender, string appName, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new ListEnvVarsHandler.Request(appName), cancellationToken);
    }
}
