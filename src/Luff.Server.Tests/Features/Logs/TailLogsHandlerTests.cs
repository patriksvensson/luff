using Luff.Server.Infrastructure;
using Luff.Server.Tests.Agents;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Logs;

public sealed class TailLogsHandlerTests
{
    [Fact]
    public async Task Should_Tail_The_Sole_Attached_Agent()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        fixture.HasConnectedAgent("agent-1");

        // When
        await fixture.TailLogs("web", agent: null);

        // Then
        fixture.Logs.ShouldSatisfyAllConditions(
            logs => logs.TailedAgent.ShouldBe("agent-1"),
            logs => logs.TailedApp.ShouldBe("web"),
            logs => logs.TailedCount.ShouldBe(200));
    }

    [Fact]
    public async Task Should_Tail_The_Named_Agent()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasAttachment("web", "agent-2");
        fixture.HasConnectedAgent("agent-2");

        // When
        await fixture.TailLogs("web", agent: "agent-2");

        // Then
        fixture.Logs.TailedAgent.ShouldBe("agent-2");
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AgentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.TailLogs("ghost", null));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }

    [Fact]
    public async Task Should_Require_An_Agent_When_Multiple_Are_Attached()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasAttachment("web", "agent-2");
        fixture.HasConnectedAgent("agent-1");
        fixture.HasConnectedAgent("agent-2");

        // When
        var exception = await Record.ExceptionAsync(() => fixture.TailLogs("web", null));

        // Then
        exception.ShouldBeOfType<AgentSelectionRequiredException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Has_No_Attachments()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() => fixture.TailLogs("web", null));

        // Then
        exception.ShouldBeOfType<AgentSelectionRequiredException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Named_Agent_Is_Not_Attached()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        fixture.HasConnectedAgent("agent-1");

        // When
        var exception = await Record.ExceptionAsync(() => fixture.TailLogs("web", "agent-2"));

        // Then
        exception.ShouldBeOfType<AttachmentNotFoundException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Target_Agent_Is_Disconnected()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        var exception = await Record.ExceptionAsync(() => fixture.TailLogs("web", "agent-1"));

        // Then
        exception.ShouldBeOfType<AgentNotConnectedException>();
    }
}
