using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class ReportAgentHealthHandlerTests
{
    [Fact]
    public async Task Should_Record_The_Reported_Health_On_The_Attachment()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.ReportHealth("agent-1",
            [new AgentHealthEntry("web", AppRuntimeHealth.Unhealthy, "restarting")]);

        // Then
        (await fixture.GetAttachment("web", "agent-1")).ShouldNotBeNull().ShouldSatisfyAllConditions(
            attachment => attachment.HealthStatus.ShouldBe(AppRuntimeHealth.Unhealthy),
            attachment => attachment.HealthDetail.ShouldBe("restarting"),
            attachment => attachment.HealthReportedAt.ShouldNotBeNull());
    }

    [Fact]
    public async Task Should_Alert_On_The_Transition_Into_Unhealthy()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.ReportHealth("agent-1",
            [new AgentHealthEntry("web", AppRuntimeHealth.Unhealthy, "restart-looping")]);

        // Then
        fixture.Alerts.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            alert => alert.Kind.ShouldBe(AlertKind.AppUnhealthy),
            alert => alert.App.ShouldBe("web"),
            alert => alert.Agent.ShouldBe("agent-1"));
    }

    [Fact]
    public async Task Should_Not_Re_Alert_While_Already_Unhealthy()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.ReportHealth("agent-1", [new AgentHealthEntry("web", AppRuntimeHealth.Unhealthy, "boom")]);

        // When — a second unhealthy report is not a transition
        await fixture.ReportHealth("agent-1", [new AgentHealthEntry("web", AppRuntimeHealth.Unhealthy, "boom")]);

        // Then
        fixture.Alerts.Published.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Ignore_Health_For_An_Unattached_App()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ReportHealth("agent-1", [new AgentHealthEntry("web", AppRuntimeHealth.Healthy, null)]));

        // Then
        exception.ShouldBeNull();
    }
}
