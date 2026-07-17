namespace Luff.Server.Features;

public sealed class ListUsersHandler : IRequestHandler<ListUsersHandler.Request, IReadOnlyList<UserResponse>>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<IReadOnlyList<UserResponse>>;

    public ListUsersHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<UserResponse>> Handle(Request request, CancellationToken cancellationToken)
    {
        var users = await _database.Users
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        return [.. users.Select(user => user.ToResponse())];
    }
}
