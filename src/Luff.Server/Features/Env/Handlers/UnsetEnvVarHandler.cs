namespace Luff.Server.Features;

public sealed class UnsetEnvVarHandler : IRequestHandler<UnsetEnvVarHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string AppName { get; }
        public string Key { get; }

        public Request(string appName, string key)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }
    }

    public UnsetEnvVarHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var existing = await _database.EnvVars.FindAsync([request.AppName, request.Key], cancellationToken)
            ?? throw new EnvVarNotFoundException(request.Key, request.AppName);

        _database.EnvVars.Remove(existing);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class UnsetEnvVarHandlerExtensions
{
    public static async Task UnsetEnvVar(this ISender sender, string appName, string key,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new UnsetEnvVarHandler.Request(appName, key), cancellationToken);
    }
}
