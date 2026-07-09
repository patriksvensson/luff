using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Volumes;

public sealed class AddVolumeHandlerTests
{
    [Fact]
    public async Task Should_Add_A_Volume()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");

        // When
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/data", "/data", false));

        // Then
        var stored = await fixture.GetVolumes("web");
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            volume => volume.Source.ShouldBe("/srv/data"),
            volume => volume.Target.ShouldBe("/data"),
            volume => volume.ReadOnly.ShouldBeFalse());
    }

    [Fact]
    public async Task Should_Upsert_By_Target()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/old", "/data", false));

        // When
        await fixture.AddVolume(new AddVolumeHandler.Request("web", "/srv/new", "/data", true));

        // Then
        var stored = await fixture.GetVolumes("web");
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            volume => volume.Source.ShouldBe("/srv/new"),
            volume => volume.ReadOnly.ShouldBeTrue());
    }

    [Fact]
    public async Task Should_Throw_When_The_Source_Is_Denied()
    {
        // Given
        using var fixture = new VolumesFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddVolume(new AddVolumeHandler.Request("web", "/var/run/docker.sock", "/sock", false)));

        // Then
        exception.ShouldBeOfType<InvalidVolumeException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new VolumesFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddVolume(new AddVolumeHandler.Request("ghost", "/srv/data", "/data", false)));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
