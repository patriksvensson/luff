namespace Luff.Server.Features;

public sealed class RefreshHandler : IRequestHandler<RefreshHandler.Request, AuthResponse>
{
    private readonly LuffDbContext _database;
    private readonly IJwtIssuer _jwt;
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<AuthResponse>
    {
        public string RefreshToken { get; }

        public Request(string refreshToken)
        {
            RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
        }
    }

    public RefreshHandler(LuffDbContext database, IJwtIssuer jwt, RefreshTokenService refreshTokens)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<AuthResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var (refresh, username) = await _refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);

        var user = await _database.Users.FindAsync([username], cancellationToken)
            ?? throw new InvalidCredentialsException();

        return new AuthResponse(_jwt.Issue(user), refresh);
    }
}

public static class RefreshHandlerExtensions
{
    public static async Task<AuthResponse> Refresh(
        this ISender sender, string refreshToken, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new RefreshHandler.Request(refreshToken), cancellationToken);
    }
}
