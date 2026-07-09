using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Fleet;

public sealed class AgentRegistryTests
{
    [Fact]
    public void Should_Register_Agent_Marked_As_Connected()
    {
        // Given
        var registry = new AgentRegistry();

        // When
        registry.MarkConnected("local", "1.2.3");

        // Then
        registry.List().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(agent =>
            {
                agent.Name.ShouldBe("local");
                agent.Status.ShouldBe(AgentConnectionStatus.Connected);
                agent.Version.ShouldBe("1.2.3");
            });
    }

    [Fact]
    public void Should_Track_A_Front_Door_Host_Across_Disconnect()
    {
        // Given
        var registry = new AgentRegistry();
        registry.MarkConnected("local", "1.2.3", hostsFrontDoor: true);

        // When
        registry.MarkDisconnected("local");

        // Then
        registry.IsFrontDoorHost("local").ShouldBeTrue();
    }

    [Fact]
    public void Should_Not_Flag_A_Regular_Agent_As_A_Front_Door_Host()
    {
        // Given
        var registry = new AgentRegistry();

        // When
        registry.MarkConnected("local", "1.2.3");

        // Then
        registry.IsFrontDoorHost("local").ShouldBeFalse();
    }

    [Fact]
    public void Should_Update_Status_Of_Disconnected_Agent()
    {
        // Given
        var registry = new AgentRegistry();
        registry.MarkConnected("local", "1.2.3");

        // When
        registry.MarkDisconnected("local");

        // Then
        registry.List().ShouldHaveSingleItem()
            .Status.ShouldBe(AgentConnectionStatus.Disconnected);
    }
}
