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
}
