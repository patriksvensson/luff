namespace Luff.Agent;

// A single http_basic account for a Caddy route. The hash is a ready-to-use bcrypt string (it starts with
// "$", so Caddy consumes it verbatim); the control plane computes it and the agent never sees the plaintext.
public sealed record BasicAuth(string Username, string Hash)
{
    public static BasicAuth? From(string? username, string? hash)
    {
        return string.IsNullOrEmpty(username) || string.IsNullOrEmpty(hash)
            ? null
            : new BasicAuth(username, hash);
    }
}
