using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Registries;

public sealed class RemoveRegistryHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Registry()
    {
        // Given
        using var fixture = new RegistriesFixture();
        await fixture.AddRegistry("ghcr.io", "user", "secret");

        // When
        await fixture.RemoveRegistry(
            new RemoveRegistryHandler.Request("ghcr.io"));

        // Then
        (await fixture.GetRegistries()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_When_The_Registry_Does_Not_Exist()
    {
        // Given
        using var fixture = new RegistriesFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.RemoveRegistry(
                new RemoveRegistryHandler.Request("ghost.io")));

        // Then
        exception.ShouldBeOfType<RegistryNotFoundException>();
    }
}
