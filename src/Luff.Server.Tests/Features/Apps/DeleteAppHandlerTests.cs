using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class DeleteAppHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_App()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.DeleteApp("web");

        // Then
        (await fixture.GetAppFromDatabase("web")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Throw_When_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.DeleteApp("ghost"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }

    [Fact]
    public async Task Should_Publish_An_App_Deleted_Event()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.DeleteApp("web", actor: "operator@example.com");

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.AppDeleted),
            evt => evt.Actor.ShouldBe("operator@example.com"),
            evt => evt.App.ShouldBe("web"));
    }
}
