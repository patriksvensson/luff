using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class AgentAppActionResultHandlerTests
{
    [Fact]
    public async Task Should_Notify_When_A_Start_Is_Confirmed()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.AppActionResult(
            "agent-1", "web", AppRunAction.Start, succeeded: true, actor: "alice@example.com");

        // Then
        fixture.Events.Published.ShouldContain(evt =>
            evt.Kind == AuditEventKind.AppStarted && evt.App == "web"
            && evt.Agent == "agent-1" && evt.Actor == "alice@example.com");
    }

    [Fact]
    public async Task Should_Notify_And_Mark_Stopped_When_A_Stop_Is_Confirmed()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.AppActionResult("agent-1", "web", AppRunAction.Stop, succeeded: true);

        // Then
        fixture.Events.Published.ShouldContain(evt =>
            evt.Kind == AuditEventKind.AppStopped && evt.App == "web" && evt.Agent == "agent-1");
        (await fixture.GetAttachment("web", "agent-1"))
            .ShouldNotBeNull().HealthStatus.ShouldBe(AppRuntimeHealth.Stopped);
    }

    [Fact]
    public async Task Should_Report_The_Failure_When_A_Start_Fails()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.AppActionResult(
            "agent-1", "web", AppRunAction.Start, succeeded: false, failureReason: "no such volume");

        // Then
        fixture.Events.Published.ShouldContain(evt =>
            evt.Kind == AuditEventKind.AppStartFailed && evt.App == "web" && evt.Agent == "agent-1");
        var attachment = (await fixture.GetAttachment("web", "agent-1")).ShouldNotBeNull();
        attachment.HealthStatus.ShouldBe(AppRuntimeHealth.Unhealthy);
        attachment.HealthDetail.ShouldBe("no such volume");
    }

    [Fact]
    public async Task Should_Report_The_Failure_When_A_Stop_Fails()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.AppActionResult(
            "agent-1", "web", AppRunAction.Stop, succeeded: false, failureReason: "still running");

        // Then
        fixture.Events.Published.ShouldContain(evt =>
            evt.Kind == AuditEventKind.AppStopFailed && evt.App == "web" && evt.Agent == "agent-1");
    }

    [Fact]
    public async Task Should_Ignore_A_Result_For_An_Unknown_Attachment()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        var result = await Record.ExceptionAsync(() =>
            fixture.AppActionResult("agent-1", "web", AppRunAction.Start, succeeded: true));

        // Then
        result.ShouldBeNull();
        fixture.Events.Published.ShouldNotContain(evt => evt.Kind == AuditEventKind.AppStarted);
    }
}
