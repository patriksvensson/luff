using Microsoft.AspNetCore.Authentication.Cookies;

namespace Luff.Server.Features;

public static class JwtAuth
{
    public const string Issuer = "luff";
    public const string Audience = "luff";
    public const string RoleClaim = "role";
    public const string AdminPolicy = "Admin";
    public const string CredentialsPolicy = "credentials";

    // Cookie scheme that carries a user between the
    // password step and the 2FA code step in the dashboard.
    public const string TwoFactorPendingScheme = "TwoFactorPending";

    public static ClaimsPrincipal CookiePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        }
        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
