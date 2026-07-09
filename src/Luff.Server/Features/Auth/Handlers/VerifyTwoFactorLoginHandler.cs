namespace Luff.Server.Features;

public sealed class VerifyTwoFactorLoginHandler : IRequestHandler<VerifyTwoFactorLoginHandler.Request, AuthResponse>
{
    private readonly TwoFactorChallenge _challenge;
    private readonly LuffDbContext _database;
    private readonly TwoFactorService _twoFactor;
    private readonly RefreshTokenService _refreshTokens;
    private readonly IJwtIssuer _jwt;

    public sealed class Request : IRequest<AuthResponse>
    {
        public string ChallengeToken { get; }
        public string Code { get; }

        public Request(string challengeToken, string code)
        {
            ChallengeToken = challengeToken ?? throw new ArgumentNullException(nameof(challengeToken));
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }
    }

    public VerifyTwoFactorLoginHandler(
        TwoFactorChallenge challenge, LuffDbContext database, TwoFactorService twoFactor,
        RefreshTokenService refreshTokens, IJwtIssuer jwt)
    {
        _challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _twoFactor = twoFactor ?? throw new ArgumentNullException(nameof(twoFactor));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
        _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
    }

    public async Task<AuthResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var username = await _challenge.ValidateAsync(request.ChallengeToken);

        var user = await _database.Users.FindAsync([username], cancellationToken)
            ?? throw new InvalidCredentialsException();

        if (!user.TwoFactorEnabled)
        {
            throw new InvalidCredentialsException();
        }

        if (!await _twoFactor.VerifyAsync(user, request.Code, cancellationToken))
        {
            throw new InvalidTwoFactorCodeException();
        }

        var refresh = await _refreshTokens.IssueAsync(user.Username, cancellationToken);
        return new AuthResponse(_jwt.Issue(user), refresh);
    }
}

public static class VerifyTwoFactorLoginHandlerExtensions
{
    public static async Task<AuthResponse> VerifyTwoFactorLogin(
        this ISender sender, string challengeToken, string code, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new VerifyTwoFactorLoginHandler.Request(challengeToken, code), cancellationToken);
    }
}
