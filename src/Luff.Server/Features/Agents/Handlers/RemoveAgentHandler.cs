namespace Luff.Server.Features;

public sealed class RemoveAgentHandler : IRequestHandler<RemoveAgentHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;

    public sealed class Request : IRequest<Unit>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public RemoveAgentHandler(LuffDbContext database, IAgentConnections connections)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
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

        return Unit.Value;
    }
}

public static class RemoveAgentHandlerExtensions
{
    public static async Task RemoveAgent(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        await sender.Send(new RemoveAgentHandler.Request(name), cancellationToken);
    }
}
