using Luff.Server.Infrastructure;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeBasicAuthHasher : IBasicAuthHasher
{
    // Deterministic so the hermetic suite can assert the exact value that crosses the link.
    public string Hash(string password)
    {
        return $"bcrypt:{password}";
    }
}
