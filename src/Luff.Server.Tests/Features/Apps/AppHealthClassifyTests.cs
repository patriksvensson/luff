using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class AppHealthClassifyTests
{
    [Fact]
    public void Should_Report_Stopped_When_The_App_Is_Stopped()
    {
        // Given
        var app = WebApp(stopped: true, currentTag: "v1");
        var attachments = new[] { Attachment("v1", AppRuntimeHealth.Healthy) };

        // When
        var result = AppHealth.Classify(app, attachments, null, inFlight: false);

        // Then
        result.State.ShouldBe(AppHealthState.Stopped);
    }

    [Fact]
    public void Should_Report_Unhealthy_When_An_Agent_Reports_Unhealthy()
    {
        // Given
        var app = WebApp(stopped: false, currentTag: "v1");
        var attachments = new[] { Attachment("v1", AppRuntimeHealth.Unhealthy) };

        // When
        var result = AppHealth.Classify(app, attachments, null, inFlight: false);

        // Then
        result.State.ShouldBe(AppHealthState.Unhealthy);
    }

    [Fact]
    public void Should_Report_Live_When_All_Agents_Are_Healthy_On_The_Intended_Tag()
    {
        // Given
        var app = WebApp(stopped: false, currentTag: "v1");
        var attachments = new[] { Attachment("v1", AppRuntimeHealth.Healthy) };

        // When
        var result = AppHealth.Classify(app, attachments, null, inFlight: false);

        // Then
        result.State.ShouldBe(AppHealthState.Live);
    }

    private static App WebApp(bool stopped, string? currentTag) => new()
    {
        Name = "web",
        Image = "nginx",
        Domain = "web.example.com",
        InternalPort = 80,
        Stopped = stopped,
        CurrentImageTag = currentTag,
    };

    private static AppAgent Attachment(string? runningTag, AppRuntimeHealth health) => new()
    {
        AppName = "web",
        AgentName = "agent-1",
        AttachedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        RunningTag = runningTag,
        HealthStatus = health,
    };
}
