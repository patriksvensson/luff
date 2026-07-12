using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shouldly;
using Xunit;

namespace Luff.Agent.Tests;

public sealed class ServerCertificatePinTests
{
    [Fact]
    public void Should_Match_The_Pin_Of_Its_Own_Public_Key()
    {
        // Given
        using var certificate = CreateCertificate();
        var pin = Convert.ToBase64String(
            SHA256.HashData(
                certificate.PublicKey.ExportSubjectPublicKeyInfo()));

        // When
        var result = ServerCertificatePin.Matches(certificate, pin);

        // Then
        result.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_The_Pin_Of_A_Different_Certificate()
    {
        // Given
        using var certificate = CreateCertificate();
        using var other = CreateCertificate();
        var otherPin = Convert.ToBase64String(
            SHA256.HashData(
                other.PublicKey.ExportSubjectPublicKeyInfo()));

        // When
        var result = ServerCertificatePin.Matches(certificate, otherPin);

        // Then
        result.ShouldBeFalse();
    }

    [Fact]
    public void Should_Reject_A_Malformed_Pin()
    {
        // Given
        using var certificate = CreateCertificate();

        // When
        var result = ServerCertificatePin.Matches(
            certificate, "not-base64!");

        // Then
        result.ShouldBeFalse();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
