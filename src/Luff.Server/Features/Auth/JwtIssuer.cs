namespace Luff.Server.Features;

public sealed class JwtIssuer : IJwtIssuer
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

    private readonly SigningCredentials _credentials;
    private readonly TimeProvider _timeProvider;

    public JwtIssuer(SymmetricSecurityKey key, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(key);
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

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
                [JwtRegisteredClaimNames.Sub] = user.Username,
                [JwtAuth.RoleClaim] = user.Role.ToString(),
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
