namespace Luff.Server.Features;

public sealed class GetTwoFactorStatusHandler
    : IRequestHandler<GetTwoFactorStatusHandler.Request, TwoFactorStatusResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<TwoFactorStatusResponse>
    {
        public string Username { get; }

        public Request(string username)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
        }
    }

    public GetTwoFactorStatusHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<TwoFactorStatusResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Username], cancellationToken)
            ?? throw new UserNotFoundException(request.Username);

        var remaining = await _database.RecoveryCodes
            .CountAsync(code => code.Username == user.Username && code.ConsumedAt == null, cancellationToken);

        return new TwoFactorStatusResponse(user.TwoFactorEnabled, remaining);
    }
}

public static class GetTwoFactorStatusHandlerExtensions
{
    public static async Task<TwoFactorStatusResponse> GetTwoFactorStatus(
        this ISender sender, string username, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new GetTwoFactorStatusHandler.Request(username), cancellationToken);
    }
}
