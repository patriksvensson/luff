namespace Luff.Server.Features;

public sealed class SetHealthCheckHandler : IRequestHandler<SetHealthCheckHandler.Request, AppResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<AppResponse>
    {
        public string Name { get; }
        public string Type { get; }
        public string? Endpoint { get; }
        public int TimeoutSeconds { get; }

        public Request(string name, string type, string? endpoint, int timeoutSeconds)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
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

        if (!Enum.TryParse<AppHealthCheckType>(request.Type, ignoreCase: true, out var type))
        {
            throw new InvalidHealthCheckException(
                $"Unknown health check type '{request.Type}'. Use docker, http, tcp, or none");
        }

        if (!app.IsCaddyFronted && type == AppHealthCheckType.Http)
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
        if (type == AppHealthCheckType.Http)
        {
            endpoint = string.IsNullOrEmpty(request.Endpoint) ? "/" : request.Endpoint;
            if (!AppHealthCheck.IsValidEndpoint(endpoint))
            {
                throw new InvalidHealthCheckException(
                    "Health check endpoint must be a path such as /healthz (letters, digits, and / . _ ~ -)");
            }
        }

        app.HealthCheckType = type;
        app.HealthCheckEndpoint = endpoint;
        app.HealthCheckTimeoutSeconds = timeout;

        await _database.SaveChangesAsync(cancellationToken);

        return app.ToResponse();
    }
}

public static class SetHealthCheckHandlerExtensions
{
    public static async Task<AppResponse> SetHealthCheck(
        this ISender sender, string name, string type, string? endpoint, int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new SetHealthCheckHandler.Request(name, type, endpoint, timeoutSeconds),
            cancellationToken);
    }
}
