using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Volumes;

public sealed class ListVolumesHandlerTests
{
    [Fact]
    public async Task Should_List_Volumes_Ordered_By_Target()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "logs", "/var/log", false));
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/data", "/data", false));

        // When
        var result = await fixture.ListVolumes(new ListVolumesHandler.Request("web"));

        // Then
        result.Select(volume => volume.Target).ShouldBe(["/data", "/var/log"]);
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new VolumesFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ListVolumes(new ListVolumesHandler.Request("ghost")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
