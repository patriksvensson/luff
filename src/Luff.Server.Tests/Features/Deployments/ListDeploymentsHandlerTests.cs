using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Deployments;

public sealed class ListDeploymentsHandlerTests
{
    [Fact]
    public async Task Should_Return_The_Apps_Deployments()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasPendingDeployment("web", "v1");

        // When
        var result = await fixture.ListDeployments("web");

        // Then
        result.ShouldHaveSingleItem()
            .Tag.ShouldBe("v1");
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new DeploymentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ListDeployments("web"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}