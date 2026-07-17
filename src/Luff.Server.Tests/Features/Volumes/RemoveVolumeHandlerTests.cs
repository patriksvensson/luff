using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Volumes;

public sealed class RemoveVolumeHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Volume()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/data", "/data", false, "admin@example.com"));

        // When
        await fixture.RemoveVolume(new RemoveVolumeHandler.Request("web", "/data", "admin@example.com"));

        // Then
        (await fixture.GetVolumes("web")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_When_The_Volume_Does_Not_Exist()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.RemoveVolume(new RemoveVolumeHandler.Request("web", "/missing", "admin@example.com")));

        // Then
        exception.ShouldBeOfType<VolumeNotFoundException>();
    }

    [Fact]
    public async Task Should_Publish_Volume_Added_And_Removed_Events()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");

        // When
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/data", "/data", false, "admin@example.com"));
        await fixture.RemoveVolume(new RemoveVolumeHandler.Request("web", "/data", "admin@example.com"));

        // Then
        fixture.Events.Published.Select(evt => (evt.Kind, evt.App, evt.Actor)).ShouldBe(
        [
            (AuditEventKind.VolumeAdded, "web", "admin@example.com"),
            (AuditEventKind.VolumeRemoved, "web", "admin@example.com"),
        ]);
    }
}
