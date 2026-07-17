namespace Luff.Server.Features;

public sealed class DeleteUserHandler : IRequestHandler<DeleteUserHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<Unit>
    {
        public string Email { get; }
        public string Actor { get; }

        public Request(string email, string actor)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public DeleteUserHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        if (user.Role == UserRole.Admin
            && await _database.Users.CountAsync(entry => entry.Role == UserRole.Admin, cancellationToken) <= 1)
        {
            throw new LastAdminException();
        }

        var recoveryCodes = await _database.RecoveryCodes
            .Where(code => code.Email == user.Email)
            .ToListAsync(cancellationToken);
        _database.RecoveryCodes.RemoveRange(recoveryCodes);

        var refreshTokens = await _database.RefreshTokens
            .Where(token => token.Email == user.Email)
            .ToListAsync(cancellationToken);
        _database.RefreshTokens.RemoveRange(refreshTokens);

        _database.Users.Remove(user);
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.UserDeleted,
            Actor = request.Actor,
            Title = $"User deleted: {user.Email}",
            Message = $"{user.Email} was removed.",
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class DeleteUserHandlerExtensions
{
    public static async Task DeleteUser(
        this ISender sender, string email, string actor, CancellationToken cancellationToken = default)
    {
        await sender.Send(new DeleteUserHandler.Request(email, actor), cancellationToken);
    }
}
