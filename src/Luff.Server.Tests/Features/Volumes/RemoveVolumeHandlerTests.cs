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
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/data", "/data", false));

        // When
        await fixture.RemoveVolume(new RemoveVolumeHandler.Request("web", "/data"));

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
            fixture.RemoveVolume(new RemoveVolumeHandler.Request("web", "/missing")));

        // Then
        exception.ShouldBeOfType<VolumeNotFoundException>();
    }
}
