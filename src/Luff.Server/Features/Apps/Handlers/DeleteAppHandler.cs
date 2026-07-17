namespace Luff.Server.Features;

public sealed class DeleteAppHandler : IRequestHandler<DeleteAppHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
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

    public DeleteAppHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        _database.Apps.Remove(app);
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.AppDeleted,
            Actor = request.Actor,
            Title = $"App deleted: {app.Name}",
            Message = $"{app.Name} was deleted.",
            App = app.Name,
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class DeleteAppHandlerExtensions
{
    public static async Task<Unit> DeleteApp(
        this ISender sender, string name, string actor, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new DeleteAppHandler.Request(name, actor), cancellationToken);
    }
}
