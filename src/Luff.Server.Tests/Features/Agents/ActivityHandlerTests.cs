using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class ActivityHandlerTests
{
    [Fact]
    public async Task Should_List_Deployments_Across_Apps_Newest_First()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web");
        await fixture.HasApp("api");
        await fixture.HasDeployment("web", "v1", DeploymentStatus.Succeeded, age: TimeSpan.FromDays(2));
        await fixture.HasDeployment("api", "v2", DeploymentStatus.Failed, age: TimeSpan.FromMinutes(5));
        await fixture.HasDeployment("web", "v2", DeploymentStatus.Succeeded, age: TimeSpan.FromMinutes(1));

        // When
        var rows = await fixture.Activity();

        // Then
        rows.Select(row => (row.App, row.Tag)).ShouldBe([("web", "v2"), ("api", "v2"), ("web", "v1")]);
        rows[1].ShouldSatisfyAllConditions(
            failed => failed.Failed.ShouldBeTrue(),
            failed => failed.Status.ShouldBe("failed"),
            failed => failed.When.ShouldBe("5m ago"));
    }
}
