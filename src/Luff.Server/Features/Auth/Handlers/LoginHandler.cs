namespace Luff.Server.Features;

public sealed class LoginHandler : IRequestHandler<LoginHandler.Request, LoginResponse>
{
    private readonly CredentialVerifier _verifier;
    private readonly IJwtIssuer _jwt;
    private readonly RefreshTokenService _refreshTokens;
    private readonly TwoFactorChallenge _challenge;

    public sealed class Request : IRequest<LoginResponse>
    {
        public string Username { get; }
        public string Password { get; }

        public Request(string username, string password)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
        }
    }

    public LoginHandler(
        CredentialVerifier verifier, IJwtIssuer jwt, RefreshTokenService refreshTokens, TwoFactorChallenge challenge)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
        _challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
    }

    public async Task<LoginResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _verifier.VerifyAsync(request.Username, request.Password, cancellationToken);

        // With 2FA on, a correct password earns only a short-lived challenge.
        // The 2FA code later trades it for tokens.
        if (user.TwoFactorEnabled)
        {
            return LoginResponse.Challenge(_challenge.Issue(user.Username));
        }

        var refresh = await _refreshTokens.IssueAsync(user.Username, cancellationToken);
        return LoginResponse.Tokens(new AuthResponse(_jwt.Issue(user), refresh));
    }
}

public static class LoginHandlerExtensions
{
    public static async Task<LoginResponse> Login(
        this ISender sender, string username, string password, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new LoginHandler.Request(username, password), cancellationToken);
    }
}
