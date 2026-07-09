using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Deployments;

public sealed class TriggerDeploymentHandlerTests
{
    [Fact]
    public async Task Should_Start_The_Deployment_When_An_Agent_Is_Attached()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        var result = await fixture.TriggerDeployment("web", "v1");

        // Then
        result.ShouldSatisfyAllConditions(
            deployment => deployment.App.ShouldBe("web"),
            deployment => deployment.Tag.ShouldBe("v1"),
            deployment => deployment.Status.ShouldBe("InProgress"));
    }

    [Fact]
    public async Task Should_Fail_The_Deployment_When_No_Agent_Is_Attached()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");

        // When
        var result = await fixture.TriggerDeployment("web", "v1");

        // Then
        result.ShouldSatisfyAllConditions(
            deployment => deployment.Status.ShouldBe("Failed"),
            deployment => deployment.FailureReason.ShouldBe("The app is not attached to any agent"));
    }

    [Fact]
    public async Task Should_Default_To_The_Current_Tag_When_None_Is_Given()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v3");

        // When
        var result = await fixture.TriggerDeployment("web", tag: null);

        // Then
        result.Tag.ShouldBe("v3");
    }

    [Fact]
    public async Task Should_Coalesce_When_A_Deployment_Is_Already_Pending()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasInProgressDeployment("web", "v0", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");

        // When
        var result = await fixture.TriggerDeployment("web", "v2");

        // Then
        result.Status.ShouldBe("Pending");
        var deployments = await fixture.GetDeployments("web");
        deployments.Where(deployment => deployment.Status == DeploymentStatus.Pending)
            .ShouldHaveSingleItem().Tag.ShouldBe("v2");
    }

    [Fact]
    public async Task Should_Throw_When_No_Tag_And_No_Current_Tag()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.TriggerDeployment("web", tag: null));

        // Then
        exception.ShouldBeOfType<DeploymentTagRequiredException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Tag_Is_Invalid()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.TriggerDeployment("web", "not a valid tag"));

        // Then
        exception.ShouldBeOfType<InvalidImageTagException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new DeploymentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.TriggerDeployment("ghost", "v1"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
