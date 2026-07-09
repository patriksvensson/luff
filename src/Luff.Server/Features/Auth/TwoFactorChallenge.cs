namespace Luff.Server.Features;

public sealed class TwoFactorChallenge
{
    private const string PurposeClaim = "purpose";
    private const string PurposeValue = "2fa";
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(5);

    private readonly SymmetricSecurityKey _key;
    private readonly SigningCredentials _credentials;
    private readonly TimeProvider _timeProvider;

    public TwoFactorChallenge(SymmetricSecurityKey key, TimeProvider timeProvider)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string Issue(string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtAuth.Issuer,
            Audience = JwtAuth.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(_lifetime),
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = username,
                [PurposeClaim] = PurposeValue,
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<string> ValidateAsync(string challengeToken)
    {
        ArgumentNullException.ThrowIfNull(challengeToken);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = JwtAuth.Issuer,
            ValidAudience = JwtAuth.Audience,
            IssuerSigningKey = _key,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            LifetimeValidator = (_, expires, _, _) =>
                expires is null || expires > _timeProvider.GetUtcNow().UtcDateTime,
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(challengeToken, parameters);
        if (!result.IsValid
            || !result.Claims.TryGetValue(PurposeClaim, out var purpose)
            || purpose as string != PurposeValue
            || !result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subject)
            || subject is not string username)
        {
            throw new TwoFactorChallengeInvalidException();
        }

        return username;
    }
}
