using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class AppsOverviewHandlerTests
{
    [Fact]
    public async Task Should_Report_The_Newest_Deployment_As_Last_Deploy()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v2");
        await fixture.HasDeployment("web", "v1", DeploymentStatus.Succeeded, age: TimeSpan.FromHours(2));
        await fixture.HasDeployment("web", "v2", DeploymentStatus.Succeeded, age: TimeSpan.FromMinutes(5));

        // When
        var overview = await fixture.Overview();

        // Then
        var web = overview.Apps.Single(app => app.Name == "web");
        web.ShouldSatisfyAllConditions(
            row => row.State.ShouldBe(AppHealthState.Live),
            row => row.CurrentTag.ShouldBe("v2"),
            row => row.MachineCount.ShouldBe(1),
            row => row.LastDeploy.ShouldBe("deployed 5m ago"));
    }

    [Fact]
    public async Task Should_Classify_An_Unattached_App_As_Dormant()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("grafana");

        // When
        var overview = await fixture.Overview();

        // Then
        var app = overview.Apps.Single();
        app.ShouldSatisfyAllConditions(
            row => row.State.ShouldBe(AppHealthState.Dormant),
            row => row.StateDetail.ShouldBe("not attached"),
            row => row.MachineCount.ShouldBe(0),
            row => row.LastDeploy.ShouldBe("never deployed"));
    }

    [Fact]
    public async Task Should_Classify_A_Behind_Attachment_As_Drift()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v2");
        await fixture.HasAttachment("web", "agent-2", runningTag: "v1");

        // When
        var overview = await fixture.Overview();

        // Then
        var web = overview.Apps.Single();
        web.ShouldSatisfyAllConditions(
            row => row.State.ShouldBe(AppHealthState.Drift),
            row => row.StateDetail.ShouldBe("1 behind"),
            row => row.MachineCount.ShouldBe(2));
    }

    [Fact]
    public async Task Should_Flag_An_In_Flight_Deploy_And_Summarize_The_Fleet()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("api", currentImageTag: "v1");
        await fixture.HasAttachment("api", "agent-1", runningTag: "v1");
        await fixture.HasDeployment("api", "v2", DeploymentStatus.InProgress);
        fixture.KnowsAgent("agent-1");
        fixture.KnowsAgent("agent-2");
        fixture.HasConnectedAgent("agent-1");

        // When
        var overview = await fixture.Overview();

        // Then
        overview.ShouldSatisfyAllConditions(
            result => result.Apps.Single().State.ShouldBe(AppHealthState.Deploying),
            result => result.DeployingCount.ShouldBe(1),
            result => result.MachineCount.ShouldBe(2),
            result => result.AllConnected.ShouldBeFalse());
    }
}
