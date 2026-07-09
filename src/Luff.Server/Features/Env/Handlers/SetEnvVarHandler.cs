namespace Luff.Server.Features;

public sealed class SetEnvVarHandler : IRequestHandler<SetEnvVarHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<Unit>
    {
        public string AppName { get; }
        public string Key { get; }
        public string Value { get; }

        public Request(string appName, string key, string value)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public SetEnvVarHandler(LuffDbContext database, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken)
                  ?? throw new AppNotFoundException(request.AppName);

        if (!EnvKeyValidator.IsValid(request.Key))
        {
            throw new InvalidEnvKeyException(request.Key);
        }

        var protectedValue = _protector.Protect(request.Value);

        var existing = await _database.EnvVars.FindAsync([app.Name, request.Key], cancellationToken);
        if (existing is null)
        {
            _database.EnvVars.Add(new EnvVar
            {
                AppName = app.Name,
                Key = request.Key,
                Value = protectedValue,
            });
        }
        else
        {
            existing.Value = protectedValue;
        }

        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class SetEnvVarHandlerExtensions
{
    public static async Task SetEnvVar(this ISender sender, string appName, string key,
        string value, CancellationToken cancellationToken = default)
    {
        await sender.Send(new SetEnvVarHandler.Request(appName, key, value), cancellationToken);
    }
}