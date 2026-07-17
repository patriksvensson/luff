namespace Luff.Server.Features;

public sealed class AgentConnectedHandler : IRequestHandler<AgentConnectedHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly DeployEngine _engine;
    private readonly FrontDoorConfigurator _frontDoor;
    private readonly FrontDoorOptions _frontDoorOptions;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string AgentName { get; }

        public Request(string agentName)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        }
    }

    public AgentConnectedHandler(
        LuffDbContext database,
        DeployEngine engine,
        FrontDoorConfigurator frontDoor,
        IOptions<FrontDoorOptions> frontDoorOptions,
        IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _frontDoor = frontDoor ?? throw new ArgumentNullException(nameof(frontDoor));
        _frontDoorOptions = frontDoorOptions?.Value ?? throw new ArgumentNullException(nameof(frontDoorOptions));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var domain = await FrontDoor.ResolveDomainAsync(_database, _frontDoorOptions, cancellationToken);
        _frontDoor.ConfigureAgentIfHost(request.AgentName, domain);

        await _engine.CatchUpAgentAsync(request.AgentName, cancellationToken);
        await _engine.ReassertRoutesAsync(request.AgentName, cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AgentConnected,
            Actor = Actors.Agent(request.AgentName),
            Title = $"Agent connected: {request.AgentName}",
            Message = $"Agent '{request.AgentName}' dialed in to the control plane.",
            Agent = request.AgentName,
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class AgentConnectedHandlerExtensions
{
    public static async Task AgentConnected(
        this IScopedSender sender, string agentName, CancellationToken cancellationToken = default)
    {
        await sender.Send(new AgentConnectedHandler.Request(agentName), cancellationToken);
    }
}
