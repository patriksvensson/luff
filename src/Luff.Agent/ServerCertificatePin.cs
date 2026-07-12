using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Luff.Agent;

public static class ServerCertificatePin
{
    public static bool Matches(X509Certificate2 certificate, string expectedPin)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (string.IsNullOrEmpty(expectedPin))
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(expectedPin);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo());
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
