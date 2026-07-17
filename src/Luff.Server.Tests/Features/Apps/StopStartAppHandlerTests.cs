using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class StopStartAppHandlerTests
{
    [Fact]
    public async Task Should_Mark_The_App_Stopped_And_Push_Stop_To_Agents()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        var result = await fixture.StopApp("web");

        // Then
        result.Stopped.ShouldBeTrue();
        (await fixture.GetAppFromDatabase("web")).ShouldNotBeNull().Stopped.ShouldBeTrue();
        reader.TryRead(out var message).ShouldBeTrue();
        message!.StopApp.App.ShouldBe("web");
    }

    [Fact]
    public async Task Should_Mark_The_Attachment_Health_Stopped()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        fixture.Agents.Register("agent-1");

        // When
        await fixture.StopApp("web");

        // Then
        (await fixture.GetAttachment("web", "agent-1"))
            .ShouldNotBeNull().HealthStatus.ShouldBe(AppRuntimeHealth.Stopped);
    }

    [Fact]
    public async Task Should_Clear_Stopped_And_Push_Start_To_Agents()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");
        await fixture.StopApp("web");
        reader.TryRead(out _);

        // When
        var result = await fixture.StartApp("web");

        // Then
        result.Stopped.ShouldBeFalse();
        reader.TryRead(out var message).ShouldBeTrue();
        message!.StartApp.App.ShouldBe("web");
    }

    [Fact]
    public async Task Should_Notify_When_An_App_Is_Stopped()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.StopApp("web", actor: "alice@example.com");

        // Then
        fixture.Events.Published.ShouldContain(evt =>
            evt.Kind == AuditEventKind.AppStopped && evt.App == "web" && evt.Actor == "alice@example.com");
    }

    [Fact]
    public async Task Should_Notify_When_An_App_Is_Started()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.StartApp("web", actor: "alice@example.com");

        // Then
        fixture.Events.Published.ShouldContain(evt =>
            evt.Kind == AuditEventKind.AppStarted && evt.App == "web" && evt.Actor == "alice@example.com");
    }
}
