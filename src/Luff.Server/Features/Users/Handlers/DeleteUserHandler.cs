namespace Luff.Server.Features;

public sealed class DeleteUserHandler : IRequestHandler<DeleteUserHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string Email { get; }

        public Request(string email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }

    public DeleteUserHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
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

        return Unit.Value;
    }
}

public static class DeleteUserHandlerExtensions
{
    public static async Task DeleteUser(
        this ISender sender, string email, CancellationToken cancellationToken = default)
    {
        await sender.Send(new DeleteUserHandler.Request(email), cancellationToken);
    }
}
