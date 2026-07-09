namespace Luff.Server.Features;

public sealed class DeleteAppHandler : IRequestHandler<DeleteAppHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string Name { get; }

        public Request(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public DeleteAppHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.Name], cancellationToken)
            ?? throw new AppNotFoundException(request.Name);

        _database.Apps.Remove(app);
        await _database.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class DeleteAppHandlerExtensions
{
    public static async Task<Unit> DeleteApp(
        this ISender sender, string name, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new DeleteAppHandler.Request(name), cancellationToken);
    }
}
