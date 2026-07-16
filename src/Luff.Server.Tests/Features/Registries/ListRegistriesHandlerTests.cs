using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Registries;

public sealed class ListRegistriesHandlerTests
{
    [Fact]
    public async Task Should_List_Registries_Ordered_By_Host()
    {
        // Given
        using var fixture = new RegistriesFixture();
        await fixture.AddRegistry("ghcr.io", "user", "secret");
        await fixture.AddRegistry("docker.io", "user", "secret");

        // When
        var result = await fixture.ListRegistries();

        // Then
        result.Select(registry => registry.Host)
            .ShouldBe(["docker.io", "ghcr.io"]);
    }

    [Fact]
    public async Task Should_Return_The_Decrypted_Password()
    {
        // Given
        using var fixture = new RegistriesFixture();
        await fixture.AddRegistry("ghcr.io", "user", "s3cret");

        // When
        var result = await fixture.ListRegistries();

        // Then
        result.ShouldHaveSingleItem().Password.ShouldBe("s3cret");
    }
}
