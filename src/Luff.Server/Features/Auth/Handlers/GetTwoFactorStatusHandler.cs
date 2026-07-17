namespace Luff.Server.Features;

public sealed class GetTwoFactorStatusHandler
    : IRequestHandler<GetTwoFactorStatusHandler.Request, TwoFactorStatusResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<TwoFactorStatusResponse>
    {
        public string Email { get; }

        public Request(string email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }

    public GetTwoFactorStatusHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<TwoFactorStatusResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        var remaining = await _database.RecoveryCodes
            .CountAsync(code => code.Email == user.Email && code.ConsumedAt == null, cancellationToken);

        return new TwoFactorStatusResponse(user.TwoFactorEnabled, remaining);
    }
}

public static class GetTwoFactorStatusHandlerExtensions
{
    public static async Task<TwoFactorStatusResponse> GetTwoFactorStatus(
        this ISender sender, string email, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new GetTwoFactorStatusHandler.Request(email), cancellationToken);
    }
}
