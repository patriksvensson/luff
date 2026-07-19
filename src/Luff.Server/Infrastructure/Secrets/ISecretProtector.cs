namespace Luff.Server.Infrastructure;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class SecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector("Luff.Secrets");
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        return _protector.Unprotect(ciphertext);
    }
}