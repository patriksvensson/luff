namespace Luff.Server.Features;

public sealed class SetHealthCheckHandler : IRequestHandler<SetHealthCheckHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }
        public AppHealthCheckType Type { get; }
        public string? Endpoint { get; }
        public int TimeoutSeconds { get; }

        public Request(string name, AppHealthCheckType type, string? endpoint, int timeoutSeconds)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Endpoint = endpoint;
            TimeoutSeconds = timeoutSeconds;
        }
    }

    public SetHealthCheckHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<AppResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        if (!app.IsCaddyFronted && request.Type == AppHealthCheckType.Http)
        {
            throw new InvalidHealthCheckException(
                "Only a web app can use an HTTP health check. Use docker, tcp, or none");
        }

        var timeout = request.TimeoutSeconds == 0 ? 300 : request.TimeoutSeconds;
        if (timeout < 0)
        {
            throw new InvalidHealthCheckException("Health check timeout must be a positive number of seconds");
        }

        string? endpoint = null;
        if (request.Type == AppHealthCheckType.Http)
        {
            endpoint = string.IsNullOrEmpty(request.Endpoint) ? "/" : request.Endpoint;
            if (!AppHealthCheck.IsValidEndpoint(endpoint))
            {
                throw new InvalidHealthCheckException(
                    "Health check endpoint must be a path such as /healthz (letters, digits, and / . _ ~ -)");
            }
        }

        app.HealthCheckType = request.Type;
        app.HealthCheckEndpoint = endpoint;
        app.HealthCheckTimeoutSeconds = timeout;

        await _database.SaveChangesAsync(cancellationToken);

        return app.ToResponse();
    }
}

public static class SetHealthCheckHandlerExtensions
{
    public static async Task<AppResponse> SetHealthCheck(
        this ISender sender, string name, AppHealthCheckType type, string? endpoint, int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new SetHealthCheckHandler.Request(name, type, endpoint, timeoutSeconds),
            cancellationToken);
    }
}
