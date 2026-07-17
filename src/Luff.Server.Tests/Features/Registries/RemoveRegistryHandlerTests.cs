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
        await fixture.RemoveRegistry("ghcr.io");

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
            fixture.RemoveRegistry("ghost.io"));

        // Then
        exception.ShouldBeOfType<RegistryNotFoundException>();
    }

    [Fact]
    public async Task Should_Publish_Registry_Added_And_Removed_Events()
    {
        // Given
        using var fixture = new RegistriesFixture();

        // When
        await fixture.AddRegistry("ghcr.io", "user", "secret", actor: "admin@example.com");
        await fixture.RemoveRegistry("ghcr.io", actor: "admin@example.com");

        // Then
        fixture.Events.Published.Select(evt => (evt.Kind, evt.Actor)).ShouldBe(
        [
            (AuditEventKind.RegistryAdded, "admin@example.com"),
            (AuditEventKind.RegistryRemoved, "admin@example.com"),
        ]);
    }
}
