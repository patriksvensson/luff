namespace Luff.Server.Infrastructure;

public sealed class AgentLinkCertificate
{
    private const string FileName = "agent-link.pfx";

    public required X509Certificate2 Certificate { get; init; }

    public required string Pin { get; init; }

    public static AgentLinkCertificate Resolve(string keysDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(keysDirectory);

        var path = Path.Combine(keysDirectory, FileName);
        if (!File.Exists(path))
        {
            Generate(path);
        }

        var certificate = X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(path), null);
        return new AgentLinkCertificate
        {
            Certificate = certificate,
            Pin = ComputePin(certificate),
        };
    }

    private static string ComputePin(X509Certificate2 certificate)
    {
        return Convert.ToBase64String(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));
    }

    private static void Generate(string path)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Luff Agent Link", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(100));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12));
    }
}
