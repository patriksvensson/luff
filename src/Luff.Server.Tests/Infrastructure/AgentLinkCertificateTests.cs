using System.Security.Cryptography;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Infrastructure;

public sealed class AgentLinkCertificateTests
{
    [Fact]
    public void Should_Generate_And_Persist_A_Certificate_On_First_Resolve()
    {
        // Given
        var directory = Directory.CreateTempSubdirectory("luff-agent-link");

        try
        {
            // When
            var result = AgentLinkCertificate.Resolve(directory.FullName);

            // Then
            File.Exists(Path.Combine(directory.FullName, "agent-link.pfx")).ShouldBeTrue();
            result.Certificate.HasPrivateKey.ShouldBeTrue();
            result.Pin.ShouldNotBeNullOrWhiteSpace();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Should_Reuse_The_Same_Key_And_Pin_Across_Resolves()
    {
        // Given
        var directory = Directory.CreateTempSubdirectory("luff-agent-link");

        try
        {
            var first = AgentLinkCertificate.Resolve(directory.FullName);

            // When
            var result = AgentLinkCertificate.Resolve(directory.FullName);

            // Then
            result.Pin.ShouldBe(first.Pin);
            result.Certificate.Thumbprint.ShouldBe(first.Certificate.Thumbprint);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Should_Derive_The_Pin_As_Base64_Sha256_Of_The_Public_Key()
    {
        // Given
        var directory = Directory.CreateTempSubdirectory("luff-agent-link");

        try
        {
            var result = AgentLinkCertificate.Resolve(directory.FullName);

            // When
            var expected = Convert.ToBase64String(
                SHA256.HashData(result.Certificate.PublicKey.ExportSubjectPublicKeyInfo()));

            // Then
            result.Pin.ShouldBe(expected);
            Convert.FromBase64String(result.Pin).Length.ShouldBe(32);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
