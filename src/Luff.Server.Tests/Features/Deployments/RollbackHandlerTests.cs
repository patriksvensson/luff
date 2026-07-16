using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Deployments;

public sealed class RollbackHandlerTests
{
    [Fact]
    public async Task Should_Start_A_Deployment_With_The_Previous_Tag()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2", previousImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1");

        // When
        var result = await fixture.Rollback("web");

        // Then
        result.ShouldSatisfyAllConditions(
            deployment => deployment.Tag.ShouldBe("v1"),
            deployment => deployment.Status.ShouldBe(DeploymentStatus.InProgress));
    }

    [Fact]
    public async Task Should_Clear_The_Stopped_State_When_Rolling_Back()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2", previousImageTag: "v1", stopped: true);
        await fixture.HasAttachment("web", "agent-1");

        // When
        var result = await fixture.Rollback("web");

        // Then
        result.Tag.ShouldBe("v1");
        (await fixture.FindApp("web")).ShouldNotBeNull().Stopped.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Coalesce_With_A_Pending_Deployment()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2", previousImageTag: "v1");
        await fixture.HasInProgressDeployment("web", "v2", "agent-1");
        await fixture.HasPendingDeployment("web", "v3");

        // When
        var result = await fixture.Rollback("web");

        // Then
        result.Status.ShouldBe(DeploymentStatus.Pending);
        (await fixture.GetDeployments("web"))
            .Where(deployment => deployment.Status == DeploymentStatus.Pending)
            .ShouldHaveSingleItem().Tag.ShouldBe("v1");
    }

    [Fact]
    public async Task Should_Throw_When_There_Is_No_Previous_Tag()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Rollback("web"));

        // Then
        exception.ShouldBeOfType<NoPreviousDeploymentException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new DeploymentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Rollback("web"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
