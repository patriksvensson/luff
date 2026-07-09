using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class AttachAppHandlerTests
{
    [Fact]
    public async Task Should_Attach_The_App_To_The_Agent()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        fixture.KnowsAgent("agent-1");

        // When
        await fixture.Attach(new AttachAppHandler.Request("agent-1", "web"));

        // Then
        (await fixture.GetAttachments("web")).ShouldHaveSingleItem()
            .AgentName.ShouldBe("agent-1");
    }

    [Fact]
    public async Task Should_Be_Idempotent()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        fixture.KnowsAgent("agent-1");
        await fixture.Attach(new AttachAppHandler.Request("agent-1", "web"));

        // When
        await fixture.Attach(new AttachAppHandler.Request("agent-1", "web"));

        // Then
        (await fixture.GetAttachments("web")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AgentsFixture();
        fixture.KnowsAgent("agent-1");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Attach(new AttachAppHandler.Request("agent-1", "ghost")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Agent_Is_Unknown()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Attach(new AttachAppHandler.Request("ghost", "web")));

        // Then
        exception.ShouldBeOfType<AgentNotFoundException>();
    }
}
