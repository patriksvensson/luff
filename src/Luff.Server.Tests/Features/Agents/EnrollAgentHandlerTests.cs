using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class EnrollAgentHandlerTests
{
    [Fact]
    public async Task Should_Mint_A_Token_And_Register_A_Pending_Agent()
    {
        // Given
        using var fixture = new AgentsFixture();

        // When
        var response = await fixture.Enroll("hetzner-fsn1");

        // Then
        response.Name.ShouldBe("hetzner-fsn1");
        response.Token.ShouldStartWith("luff_");

        var fleet = await fixture.Fleet();
        fleet.ShouldHaveSingleItem();
        fleet[0].Status.ShouldBe(AgentConnectionStatus.Pending);
    }

    [Fact]
    public async Task Should_Reject_A_Duplicate_Name()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.Enroll("web1");

        // When
        var exception = await Record.ExceptionAsync(() => fixture.Enroll("web1"));

        // Then
        exception.ShouldBeOfType<AgentAlreadyExistsException>();
    }

    [Fact]
    public async Task Should_Publish_A_Machine_Enrolled_Event()
    {
        // Given
        using var fixture = new AgentsFixture();

        // When
        await fixture.Enroll("hetzner-fsn1", actor: "admin@example.com");

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.AgentEnrolled),
            evt => evt.Actor.ShouldBe("admin@example.com"),
            evt => evt.Agent.ShouldBe("hetzner-fsn1"));
    }
}
