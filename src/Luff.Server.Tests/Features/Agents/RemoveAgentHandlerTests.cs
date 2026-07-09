using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class RemoveAgentHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Agent_And_Detach_Its_Apps()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasAgent("host1", lastSeenAt: fixture.Time.GetUtcNow());
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "host1");

        // When
        await fixture.RemoveAgent("host1");

        // Then
        (await fixture.Fleet()).ShouldBeEmpty();
        (await fixture.GetAttachments("web")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Reject_Removing_An_Unknown_Agent()
    {
        // Given
        using var fixture = new AgentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.RemoveAgent("nope"));

        // Then
        exception.ShouldBeOfType<AgentNotFoundException>();
    }
}
