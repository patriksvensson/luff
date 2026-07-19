namespace Luff.Agent;

public sealed record BasicAuth(string Username, string Hash)
{
    public static BasicAuth? From(string? username, string? hash)
    {
        return string.IsNullOrEmpty(username) || string.IsNullOrEmpty(hash)
            ? null
            : new BasicAuth(username, hash);
    }
}
