using Luff.Server.Infrastructure;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plaintext)
    {
        return $"protected:{plaintext}";
    }

    public string Unprotect(string ciphertext)
    {
        return ciphertext["protected:".Length..];
    }
}
