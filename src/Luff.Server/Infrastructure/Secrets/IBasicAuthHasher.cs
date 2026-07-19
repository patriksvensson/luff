namespace Luff.Server.Infrastructure;

public interface IBasicAuthHasher
{
    string Hash(string password);
}

public sealed class BasicAuthHasher : IBasicAuthHasher
{
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
