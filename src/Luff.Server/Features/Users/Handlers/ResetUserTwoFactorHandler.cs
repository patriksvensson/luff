namespace Luff.Server.Features;

public sealed class ResetUserTwoFactorHandler : IRequestHandler<ResetUserTwoFactorHandler.Request, Unit>
{
    private readonly LuffDbContext _database;
    private readonly RefreshTokenService _refreshTokens;
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

    public ResetUserTwoFactorHandler(
        LuffDbContext database, RefreshTokenService refreshTokens, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;

        var codes = await _database.RecoveryCodes
            .Where(code => code.Email == user.Email)
            .ToListAsync(cancellationToken);
        _database.RecoveryCodes.RemoveRange(codes);

        await _database.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(user.Email, cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.TwoFactorDisabled,
            Actor = request.Actor,
            Title = $"2FA reset: {user.Email}",
            Message = $"Two-factor authentication for {user.Email} was reset by an admin.",
        }, cancellationToken);

        return Unit.Value;
    }
}

public static class ResetUserTwoFactorHandlerExtensions
{
    public static async Task ResetUserTwoFactor(
        this ISender sender, string email, string actor, CancellationToken cancellationToken = default)
    {
        await sender.Send(new ResetUserTwoFactorHandler.Request(email, actor), cancellationToken);
    }
}
