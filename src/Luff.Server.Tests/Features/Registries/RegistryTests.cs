using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Registries;

public sealed class RegistryTests
{
    [Theory]
    [InlineData("ghcr.io/owner/app", "ghcr.io")]
    [InlineData("registry.example.com:5000/app", "registry.example.com:5000")]
    [InlineData("localhost:5000/web", "localhost:5000")]
    [InlineData("myregistry.azurecr.io/app", "myregistry.azurecr.io")]
    public void Should_Extract_The_Registry_Host(string image, string expected)
    {
        // When
        var host = Registry.ParseHost(image);

        // Then
        host.ShouldBe(expected);
    }

    [Theory]
    [InlineData("nginx")]
    [InlineData("nginx:1.27")]
    [InlineData("library/nginx")]
    public void Should_Return_Null_For_Docker_Hub_Images(string image)
    {
        // When
        var host = Registry.ParseHost(image);

        // Then
        host.ShouldBeNull();
    }
}
