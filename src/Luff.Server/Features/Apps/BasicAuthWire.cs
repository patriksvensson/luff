namespace Luff.Server.Features;

public static class BasicAuthWire
{
    public static (string Username, string Hash) Resolve(App app, IBasicAuthHasher hasher, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(protector);

        if (!app.IsCaddyFronted
            || string.IsNullOrEmpty(app.BasicAuthUsername)
            || string.IsNullOrEmpty(app.BasicAuthPassword))
        {
            return (string.Empty, string.Empty);
        }

        // The on-the-wire basic-auth credential for an app: its username plus a freshly computed bcrypt hash of the
        // stored (encrypted) password. Only the hash crosses the agent link; the plaintext never leaves the control
        // plane. Empty when the app has no basic auth, or isn't Caddy-fronted (a frontless app has no route to gate).
        return (app.BasicAuthUsername, hasher.Hash(protector.Unprotect(app.BasicAuthPassword)));
    }
}
