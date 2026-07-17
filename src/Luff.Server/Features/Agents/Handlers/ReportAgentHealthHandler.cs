namespace Luff.Server.Features;

public sealed record AgentHealthEntry(string App, AppRuntimeHealth Status, string? Detail);

public sealed class ReportAgentHealthHandler : IRequestHandler<ReportAgentHealthHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;
    private readonly TimeProvider _timeProvider;

    public sealed class Request : IRequest<Unit>
    {
        public string AgentName { get; }
        public IReadOnlyList<AgentHealthEntry> Entries { get; }

        public Request(string agentName, IReadOnlyList<AgentHealthEntry> entries)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }
    }

    public ReportAgentHealthHandler(LuffDbContext database, IEventPublisher events, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        if (request.Entries.Count == 0)
        {
            return Unit.Value;
        }

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AgentName == request.AgentName)
            .ToListAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var byApp = attachments.ToDictionary(attachment => attachment.AppName, StringComparer.Ordinal);

        var changed = false;
        var newlyUnhealthy = new List<AgentHealthEntry>();
        foreach (var entry in request.Entries)
        {
            if (!byApp.TryGetValue(entry.App, out var attachment))
            {
                continue;
            }

            // Alert only on the transition into unhealthy, so the periodic reports don't spam.
            if (entry.Status == AppRuntimeHealth.Unhealthy && attachment.HealthStatus != AppRuntimeHealth.Unhealthy)
            {
                newlyUnhealthy.Add(entry);
            }

            attachment.HealthStatus = entry.Status;
            attachment.HealthDetail = string.IsNullOrEmpty(entry.Detail) ? null : entry.Detail;
            attachment.HealthReportedAt = now;
            changed = true;
        }

        if (changed)
        {
            await _database.SaveChangesAsync(cancellationToken);
        }

        foreach (var entry in newlyUnhealthy)
        {
            await _events.PublishAsync(new AuditEvent
            {
                Kind = AuditEventKind.AppUnhealthy,
                Actor = Actors.Agent(request.AgentName),
                Title = $"App unhealthy: {entry.App}",
                Message = $"{entry.App} on {request.AgentName} reported unhealthy"
                    + (string.IsNullOrEmpty(entry.Detail) ? "." : $": {entry.Detail}."),
                App = entry.App,
                Agent = request.AgentName,
            }, cancellationToken);
        }

        return Unit.Value;
    }
}

public static class ReportAgentHealthHandlerExtensions
{
    public static async Task ReportAgentHealth(
        this IScopedSender sender, string agentName, IReadOnlyList<AgentHealthEntry> entries,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new ReportAgentHealthHandler.Request(agentName, entries), cancellationToken);
    }
}
