using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class DetachAppHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Attachment_And_Tell_The_Agent_To_Clean_Up()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        fixture.HasConnectedAgent("agent-1");

        // When
        await fixture.Detach(new DetachAppHandler.Request("agent-1", "web"));

        // Then
        (await fixture.GetAttachments("web")).ShouldBeEmpty();
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);
        message.ShouldNotBeNull().Remove.ShouldSatisfyAllConditions(
            remove => remove.App.ShouldBe("web"),
            remove => remove.Domain.ShouldBe("web.example.com"));
    }

    [Fact]
    public async Task Should_Throw_When_Not_Attached()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Detach(new DetachAppHandler.Request("agent-1", "web")));

        // Then
        exception.ShouldBeOfType<AttachmentNotFoundException>();
    }
}
