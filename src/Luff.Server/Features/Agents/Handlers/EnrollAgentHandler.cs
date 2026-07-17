namespace Luff.Server.Features;

public sealed class EnrollAgentHandler : IRequestHandler<EnrollAgentHandler.Request, EnrollAgentResponse>
{
    private readonly LuffDbContext _database;
    private readonly TimeProvider _time;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<EnrollAgentResponse>
    {
        public string Name { get; }
        public string Actor { get; }

        public Request(string name, string actor)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public EnrollAgentHandler(LuffDbContext database, TimeProvider time, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<EnrollAgentResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _database.Agents.AnyAsync(agent => agent.Name == name, cancellationToken))
        {
            throw new AgentAlreadyExistsException(name);
        }

        var token = Agent.GenerateToken();
        _database.Agents.Add(new Agent
        {
            Name = name,
            EnrollmentTokenHash = Agent.Hash(token),
            RegisteredAt = _time.GetUtcNow(),
            LastSeenAt = null,
        });
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AgentEnrolled,
            Actor = request.Actor,
            Title = $"Machine enrolled: {name}",
            Message = $"Agent '{name}' was enrolled and issued a token.",
            Agent = name,
        }, cancellationToken);

        return new EnrollAgentResponse(name, token);
    }
}

public static class EnrollAgentHandlerExtensions
{
    public static async Task<EnrollAgentResponse> EnrollAgent(
        this ISender sender, string name, string actor, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new EnrollAgentHandler.Request(name, actor), cancellationToken);
    }
}
