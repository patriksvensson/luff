using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class AppStatusHandlerTests
{
    [Fact]
    public async Task Should_Report_Per_Agent_State_And_Drift()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v2");
        await fixture.HasAttachment("web", "agent-2", runningTag: "v1");
        fixture.HasConnectedAgent("agent-1");

        // When
        var status = await fixture.Status(
            new AppStatusHandler.Request("web"));

        // Then
        status.CurrentImageTag.ShouldBe("v2");
        status.Agents.ShouldSatisfyAllConditions(
            agents => agents.Count.ShouldBe(2),
            agents => agents.Single(agent => agent.Agent == "agent-1").ShouldSatisfyAllConditions(
                first => first.RunningTag.ShouldBe("v2"),
                first => first.Connected.ShouldBeTrue()),
            agents => agents.Single(agent => agent.Agent == "agent-2").ShouldSatisfyAllConditions(
                second => second.RunningTag.ShouldBe("v1"),
                second => second.Connected.ShouldBeFalse()));
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AgentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Status(new AppStatusHandler.Request("ghost")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
