using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class ListAppsHandlerTests
{
    [Fact]
    public async Task Should_Return_Apps_Ordered_By_Name()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasApp("api");

        // When
        var result = await fixture.ListApps();

        // Then
        result.Select(app => app.Name).ShouldBe(["api", "web"]);
    }

    [Fact]
    public async Task Should_Return_Empty_When_There_Are_No_Apps()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.ListApps();

        // Then
        result.ShouldBeEmpty();
    }
}
