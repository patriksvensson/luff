namespace Luff.Server.Features;

public sealed class LogoutHandler : IRequestHandler<LogoutHandler.Request, Unit>
{
    private readonly RefreshTokenService _refreshTokens;

    public sealed class Request : IRequest<Unit>
    {
        public string RefreshToken { get; }

        public Request(string refreshToken)
        {
            RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
        }
    }

    public LogoutHandler(RefreshTokenService refreshTokens)
    {
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        await _refreshTokens.RevokeByTokenAsync(request.RefreshToken, cancellationToken);
        return Unit.Value;
    }
}

public static class LogoutHandlerExtensions
{
    public static async Task Logout(
        this ISender sender, string refreshToken, CancellationToken cancellationToken = default)
    {
        await sender.Send(new LogoutHandler.Request(refreshToken), cancellationToken);
    }
}
