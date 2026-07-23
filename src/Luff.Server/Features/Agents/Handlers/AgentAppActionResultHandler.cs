namespace Luff.Server.Features;

public sealed class AgentAppActionResultHandler : IRequestHandler<AgentAppActionResultHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;
    private readonly TimeProvider _timeProvider;

    public sealed class Request : IRequest<Unit>
    {
        public string AgentName { get; }
        public string App { get; }
        public AppRunAction Action { get; }
        public string Actor { get; }
        public bool Succeeded { get; }
        public string? FailureReason { get; }

        public Request(
            string agentName, string app, AppRunAction action, string actor, bool succeeded, string? failureReason)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            App = app ?? throw new ArgumentNullException(nameof(app));
            Action = action;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Succeeded = succeeded;
            FailureReason = failureReason;
        }
    }

    public AgentAppActionResultHandler(LuffDbContext database, IEventPublisher events, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var attachment = await _database.AppAgents.FindAsync([request.App, request.AgentName], cancellationToken);
        if (attachment is null)
        {
            return Unit.Value;
        }

        if (request.Succeeded)
        {
            if (request.Action == AppRunAction.Stop)
            {
                attachment.HealthStatus = AppRuntimeHealth.Stopped;
                attachment.HealthDetail = null;
                attachment.HealthReportedAt = _timeProvider.GetUtcNow();
                await _database.SaveChangesAsync(cancellationToken);
            }

            await _events.PublishAsync(SuccessEvent(request), cancellationToken);
            return Unit.Value;
        }

        attachment.HealthStatus = AppRuntimeHealth.Unhealthy;
        attachment.HealthDetail = request.FailureReason;
        attachment.HealthReportedAt = _timeProvider.GetUtcNow();
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(FailureEvent(request), cancellationToken);
        return Unit.Value;
    }

    private static AuditEvent SuccessEvent(Request request)
    {
        return request.Action == AppRunAction.Stop
            ? new AuditEvent
            {
                Kind = AuditEventKind.AppStopped,
                Actor = request.Actor,
                Title = $"App stopped: {request.App}",
                Message = $"{request.App} was stopped on {request.AgentName}.",
                App = request.App,
                Agent = request.AgentName,
            }
            : new AuditEvent
            {
                Kind = AuditEventKind.AppStarted,
                Actor = request.Actor,
                Title = $"App started: {request.App}",
                Message = $"{request.App} was started on {request.AgentName}.",
                App = request.App,
                Agent = request.AgentName,
            };
    }

    private static AuditEvent FailureEvent(Request request)
    {
        var reason = string.IsNullOrEmpty(request.FailureReason) ? "." : $": {request.FailureReason}.";
        return request.Action == AppRunAction.Stop
            ? new AuditEvent
            {
                Kind = AuditEventKind.AppStopFailed,
                Actor = request.Actor,
                Title = $"App stop failed: {request.App}",
                Message = $"{request.App} failed to stop on {request.AgentName}{reason}",
                App = request.App,
                Agent = request.AgentName,
            }
            : new AuditEvent
            {
                Kind = AuditEventKind.AppStartFailed,
                Actor = request.Actor,
                Title = $"App start failed: {request.App}",
                Message = $"{request.App} failed to start on {request.AgentName}{reason}",
                App = request.App,
                Agent = request.AgentName,
            };
    }
}

public static class AgentAppActionResultHandlerExtensions
{
    public static async Task AgentAppActionResult(
        this IScopedSender sender, string agentName, string app, AppRunAction action, string actor,
        bool succeeded, string? failureReason, CancellationToken cancellationToken = default)
    {
        await sender.Send(
            new AgentAppActionResultHandler.Request(agentName, app, action, actor, succeeded, failureReason),
            cancellationToken);
    }
}
