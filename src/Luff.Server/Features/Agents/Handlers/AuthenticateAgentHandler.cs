namespace Luff.Server.Features;

public sealed class AuthenticateAgentHandler : IRequestHandler<AuthenticateAgentHandler.Request, bool>
{
    private readonly LuffDbContext _database;
    private readonly AgentEnrollmentValidator _bootstrap;
    private readonly TimeProvider _time;

    public sealed class Request : IRequest<bool>
    {
        public string Name { get; }
        public string? PresentedSecret { get; }

        public Request(string name, string? presentedSecret)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            PresentedSecret = presentedSecret;
        }
    }

    public AuthenticateAgentHandler(LuffDbContext database, AgentEnrollmentValidator bootstrap, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task<bool> Handle(Request request, CancellationToken cancellationToken)
    {
        var agent = await _database.Agents.FindAsync([request.Name], cancellationToken);

        if (agent?.EnrollmentTokenHash is not null)
        {
            var presentedHash = Agent.Hash(request.PresentedSecret ?? string.Empty);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(presentedHash),
                    Encoding.UTF8.GetBytes(agent.EnrollmentTokenHash)))
            {
                return false;
            }

            agent.LastSeenAt = _time.GetUtcNow();
            await _database.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (!_bootstrap.IsValid(request.PresentedSecret))
        {
            return false;
        }

        if (agent is null)
        {
            agent = new Agent
            {
                Name = request.Name,
                EnrollmentTokenHash = null,
                RegisteredAt = _time.GetUtcNow(),
            };
            _database.Agents.Add(agent);
        }

        agent.LastSeenAt = _time.GetUtcNow();
        await _database.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public static class AuthenticateAgentHandlerExtensions
{
    public static async Task<bool> AuthenticateAgent(
        this IScopedSender sender, string name, string? presentedSecret,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(new AuthenticateAgentHandler.Request(name, presentedSecret), cancellationToken);
    }
}
