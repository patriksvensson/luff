using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class FleetOverviewHandlerTests
{
    [Fact]
    public async Task Should_Report_Pending_Connected_And_Disconnected()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasAgent("pending1");
        await fixture.HasAgent("live1", lastSeenAt: fixture.Time.GetUtcNow());
        fixture.Registry.MarkConnected("live1", "1.2.0");
        await fixture.HasAgent("gone1", lastSeenAt: fixture.Time.GetUtcNow() - TimeSpan.FromMinutes(5));

        // When
        var fleet = await fixture.Fleet();

        // Then
        fleet.Single(agent => agent.Name == "pending1").Status.ShouldBe("pending");
        fleet.Single(agent => agent.Name == "live1").ShouldSatisfyAllConditions(
            live => live.Status.ShouldBe("connected"),
            live => live.Version.ShouldBe("1.2.0"));
        fleet.Single(agent => agent.Name == "gone1").ShouldSatisfyAllConditions(
            gone => gone.Status.ShouldBe("disconnected"),
            gone => gone.LastSeen.ShouldBe("5m ago"));
    }

    [Fact]
    public async Task Should_List_A_Machines_Attached_Apps()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasAgent("host1", lastSeenAt: fixture.Time.GetUtcNow());
        fixture.Registry.MarkConnected("host1", "1.0.0");
        await fixture.HasApp("web");
        await fixture.HasApp("api");
        await fixture.HasAttachment("web", "host1");
        await fixture.HasAttachment("api", "host1");

        // When
        var fleet = await fixture.Fleet();

        // Then
        fleet.Single().Apps.ShouldBe(["api", "web"]);
    }
}
