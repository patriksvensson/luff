namespace Luff.Server.Infrastructure;

public interface IBasicAuthHasher
{
    string Hash(string password);
}
