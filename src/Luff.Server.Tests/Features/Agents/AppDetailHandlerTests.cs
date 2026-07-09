using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Agents;

public sealed class AppDetailHandlerTests
{
    [Fact]
    public async Task Should_Report_Config_Machines_And_Drift()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2", previousImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v2");
        await fixture.HasAttachment("web", "agent-2", runningTag: "v1");
        fixture.HasConnectedAgent("agent-1");
        fixture.Registry.MarkConnected("agent-1", "0.4.0", hostsFrontDoor: true);

        // When
        var detail = await fixture.Detail("web");

        // Then
        detail.ShouldSatisfyAllConditions(
            app => app.Image.ShouldBe("nginx"),
            app => app.InternalPort.ShouldBe(80),
            app => app.CurrentTag.ShouldBe("v2"),
            app => app.PreviousTag.ShouldBe("v1"),
            app => app.State.ShouldBe(AppHealthState.Drift),
            app => app.BehindCount.ShouldBe(1),
            app => app.Machines.Count.ShouldBe(2));

        detail.Machines.Single(machine => machine.Agent == "agent-1").ShouldSatisfyAllConditions(
            machine => machine.Connected.ShouldBeTrue(),
            machine => machine.Behind.ShouldBeFalse(),
            machine => machine.FrontDoor.ShouldBeTrue());

        detail.Machines.Single(machine => machine.Agent == "agent-2").ShouldSatisfyAllConditions(
            machine => machine.Connected.ShouldBeFalse(),
            machine => machine.Behind.ShouldBeTrue(),
            machine => machine.FrontDoor.ShouldBeFalse());
    }

    [Fact]
    public async Task Should_List_Recent_Deployments_Newest_First()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasDeployment("web", "v1", DeploymentStatus.Succeeded, age: TimeSpan.FromHours(2));
        await fixture.HasDeployment("web", "v2", DeploymentStatus.Failed, age: TimeSpan.FromMinutes(5));

        // When
        var detail = await fixture.Detail("web");

        // Then
        detail.Deployments.Count.ShouldBe(2);
        detail.Deployments[0].ShouldSatisfyAllConditions(
            deployment => deployment.Tag.ShouldBe("v2"),
            deployment => deployment.Failed.ShouldBeTrue(),
            deployment => deployment.When.ShouldBe("5m ago"));
        detail.Deployments[1].Tag.ShouldBe("v1");
    }

    [Fact]
    public async Task Should_Derive_Tls_From_The_Mode_And_Domain()
    {
        // Given
        using var fixture = new AgentsFixture();
        await fixture.HasApp("docs", domain: "docs.203.0.113.10.sslip.io");
        await fixture.HasApp("web", domain: "app.example.com");
        await fixture.HasApp("lb", domain: "lb.example.com", tlsMode: TlsMode.External);

        // When
        var docs = await fixture.Detail("docs");
        var web = await fixture.Detail("web");
        var lb = await fixture.Detail("lb");

        // Then
        docs.ShouldSatisfyAllConditions(
            app => app.AutoDomain.ShouldBeTrue(),
            app => app.Https.ShouldBeFalse(),
            app => app.TlsTrusted.ShouldBeFalse(),
            app => app.TlsLabel.ShouldBe("Plain HTTP"));
        web.ShouldSatisfyAllConditions(
            app => app.Https.ShouldBeTrue(),
            app => app.TlsTrusted.ShouldBeTrue(),
            app => app.TlsLabel.ShouldBe("Publicly trusted"));
        lb.ShouldSatisfyAllConditions(
            app => app.Https.ShouldBeTrue(),
            app => app.TlsTrusted.ShouldBeFalse(),
            app => app.TlsLabel.ShouldBe("External"));
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AgentsFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.Detail("ghost"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
