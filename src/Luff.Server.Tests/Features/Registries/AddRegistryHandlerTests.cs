using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Registries;

public sealed class AddRegistryHandlerTests
{
    [Fact]
    public async Task Should_Store_The_Password_Encrypted()
    {
        // Given
        using var fixture = new RegistriesFixture();

        // When
        await fixture.AddRegistry("ghcr.io", "user", "secret");

        // Then
        var stored = await fixture.GetRegistries();
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            registry => registry.Host.ShouldBe("ghcr.io"),
            registry => registry.Password.ShouldBe(fixture.Protector.Protect("secret")),
            registry => registry.Password.ShouldNotBe("secret"));
    }

    [Fact]
    public async Task Should_Not_Return_The_Password()
    {
        // Given
        using var fixture = new RegistriesFixture();

        // When
        var result = await fixture.AddRegistry(
            "ghcr.io", "user", "secret");

        // Then
        result.ShouldSatisfyAllConditions(
            response => response.Host.ShouldBe("ghcr.io"),
            response => response.Username.ShouldBe("user"));
    }

    [Fact]
    public async Task Should_Upsert_An_Existing_Host()
    {
        // Given
        using var fixture = new RegistriesFixture();
        await fixture.AddRegistry("ghcr.io", "old", "old-pass");

        // When
        await fixture.AddRegistry("ghcr.io", "new", "new-pass");

        // Then
        var stored = await fixture.GetRegistries();
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            registry => registry.Username.ShouldBe("new"),
            registry => registry.Password.ShouldBe(fixture.Protector.Protect("new-pass")));
    }
}
