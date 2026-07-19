using Luff.Server.Infrastructure;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeBasicAuthHasher : IBasicAuthHasher
{
    public string Hash(string password)
    {
        return $"bcrypt:{password}";
    }
}
