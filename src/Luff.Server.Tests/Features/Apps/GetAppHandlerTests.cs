using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class GetAppHandlerTests
{
    [Fact]
    public async Task Should_Return_The_App()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        var result = await fixture.GetApp("web");

        // Then
        result.Name.ShouldBe("web");
    }

    [Fact]
    public async Task Should_Throw_When_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.GetApp("web"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
