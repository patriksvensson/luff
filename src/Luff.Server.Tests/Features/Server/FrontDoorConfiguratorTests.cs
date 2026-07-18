using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Server;

public sealed class FrontDoorConfiguratorTests
{
    [Fact]
    public void Should_Configure_A_Front_Door_Host_With_The_Upstream()
    {
        // Given
        using var fixture = new ServerFixture();
        fixture.Agents.Register("agent-1");
        fixture.Registry.MarkConnected("agent-1", "1.0.0", hostsFrontDoor: true);

        // When
        fixture.FrontDoor.ConfigureAgentIfHost("agent-1", "cp.example.com");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out var message).ShouldBeTrue();
        message.ConfigureFrontDoor.ShouldSatisfyAllConditions(
            frontDoor => frontDoor.Domain.ShouldBe("cp.example.com"),
            frontDoor => frontDoor.Upstream.ShouldBe("host.docker.internal:8080"),
            frontDoor => frontDoor.ManagedTls.ShouldBeTrue());
    }

    [Fact]
    public void Should_Not_Configure_An_Agent_That_Does_Not_Host_The_Front_Door()
    {
        // Given
        using var fixture = new ServerFixture();
        fixture.Agents.Register("agent-1");
        fixture.Registry.MarkConnected("agent-1", "1.0.0", hostsFrontDoor: false);

        // When
        fixture.FrontDoor.ConfigureAgentIfHost("agent-1", "cp.example.com");

        // Then
        fixture.Agents.GetChannel("agent-1")
            .TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Configure_Only_The_Front_Door_Hosts_When_Reconfiguring()
    {
        // Given
        using var fixture = new ServerFixture();
        fixture.Agents.Register("agent-1");
        fixture.Agents.Register("agent-2");
        fixture.Registry.MarkConnected("agent-1", "1.0.0", hostsFrontDoor: true);
        fixture.Registry.MarkConnected("agent-2", "1.0.0", hostsFrontDoor: false);

        // When
        fixture.FrontDoor.ConfigureConnected("cp.example.com");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out _).ShouldBeTrue();
        fixture.Agents.GetChannel("agent-2").TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Manage_Tls_For_An_Auto_Domain()
    {
        // Given
        using var fixture = new ServerFixture();
        fixture.Agents.Register("agent-1");
        fixture.Registry.MarkConnected("agent-1", "1.0.0", hostsFrontDoor: true);

        // When
        fixture.FrontDoor.ConfigureAgentIfHost("agent-1", "127.0.0.1.sslip.io");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out var message).ShouldBeTrue();
        message.ConfigureFrontDoor.ManagedTls.ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Manage_Tls_For_A_Bare_Ip()
    {
        // Given
        using var fixture = new ServerFixture();
        fixture.Agents.Register("agent-1");
        fixture.Registry.MarkConnected("agent-1", "1.0.0", hostsFrontDoor: true);

        // When
        fixture.FrontDoor.ConfigureAgentIfHost("agent-1", "203.0.113.10");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out var message).ShouldBeTrue();
        message.ConfigureFrontDoor.ManagedTls.ShouldBeFalse();
    }
}
