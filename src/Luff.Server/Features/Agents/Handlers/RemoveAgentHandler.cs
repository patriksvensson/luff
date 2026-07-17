namespace Luff.Server.Features;

public sealed class RemoveAgentHandler : IRequestHandler<RemoveAgentHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly AgentRegistry _registry;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string Name { get; }
        public string Actor { get; }

        public Request(string name, string actor)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public RemoveAgentHandler(
        LuffDbContext database, IAgentConnections connections, AgentRegistry registry, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var agent = await _database.Agents.FindAsync([request.Name], cancellationToken)
                    ?? throw new AgentNotFoundException(request.Name);

        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AgentName == request.Name)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            var app = await _database.Apps.FindAsync([attachment.AppName], cancellationToken);
            if (app is not null)
            {
                _connections.TrySend(request.Name, new ControlMessage
                {
                    Remove = new Remove
                    {
                        App = app.Name,
                        Domain = app.Domain ?? string.Empty,
                    },
                });
            }
        }

        _database.AppAgents.RemoveRange(attachments);
        _database.Agents.Remove(agent);
        await _database.SaveChangesAsync(cancellationToken);

        _registry.Remove(request.Name);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AgentRemoved,
            Actor = request.Actor,
            Title = $"Machine removed: {agent.Name}",
            Message = $"Agent '{agent.Name}' was removed from the fleet.",
            Agent = agent.Name,
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class RemoveAgentHandlerExtensions
{
    public static async Task RemoveAgent(
        this ISender sender, string name, string actor, CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveAgentHandler.Request(name, actor), cancellationToken);
    }
}
