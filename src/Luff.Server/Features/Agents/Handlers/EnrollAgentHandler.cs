namespace Luff.Server.Features;

public sealed class EnrollAgentHandler : IRequestHandler<EnrollAgentHandler.Request, EnrollAgentResponse>
{
    private readonly LuffDbContext _database;
    private readonly TimeProvider _time;

    public sealed class Request : IRequest<EnrollAgentResponse>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public EnrollAgentHandler(LuffDbContext database, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _time = time ?? throw new ArgumentNullException(nameof(time));
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

        return new EnrollAgentResponse(name, token);
    }
}

public static class EnrollAgentHandlerExtensions
{
    public static async Task<EnrollAgentResponse> EnrollAgent(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new EnrollAgentHandler.Request(name), cancellationToken);
    }
}
