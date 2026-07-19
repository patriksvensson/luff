namespace Luff.Server.Infrastructure;

// Caddy's http_basic provider verifies against bcrypt hashes, and the BCL ships no bcrypt, so this is the one
// place we lean on BCrypt.Net. Only the resulting hash ever crosses the agent link; the plaintext stays here.
public sealed class BasicAuthHasher : IBasicAuthHasher
{
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
