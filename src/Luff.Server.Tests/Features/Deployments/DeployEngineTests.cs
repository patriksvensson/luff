using Luff.Protobuf;
using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Luff.Testing.Extensions;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Deployments;

public sealed class DeployEngineTests
{
    [Fact]
    public async Task Should_Send_A_Deploy_To_The_Attached_Agent()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1")
            .TryRead(out var message);

        // Then
        message.ShouldNotBeNull()
            .Deploy.Tag.ShouldBe("v1");
    }

    [Fact]
    public async Task Should_Carry_The_Managed_Route_For_A_Real_Domain()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.TlsRoute.ShouldBe(TlsRoute.Managed);
    }

    [Fact]
    public async Task Should_Carry_The_Http_Route_For_An_Sslip_Domain()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", domain: "web.127.0.0.1.sslip.io");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.TlsRoute.ShouldBe(TlsRoute.Http);
    }

    [Fact]
    public async Task Should_Carry_The_External_Route_For_An_External_App()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", tlsMode: TlsMode.External);
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.TlsRoute.ShouldBe(TlsRoute.External);
    }

    [Fact]
    public async Task Should_Record_The_Running_Deployment_Id_On_Success()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, failureReason: null);

        // Then
        (await fixture.FindAttachment("web", "agent-1"))!.RunningDeploymentId.ShouldBe(deploymentId);
    }

    [Fact]
    public async Task Should_Reassert_An_Up_To_Date_Route_On_Reconnect()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        var deploymentId = Guid.NewGuid();
        await fixture.HasApp("web", currentImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v1", runningDeploymentId: deploymentId);

        // When
        await fixture.DeployEngine.ReassertRoutesAsync("agent-1");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out var message).ShouldBeTrue();
        message.AssertRoute.ShouldSatisfyAllConditions(
            assert => assert.App.ShouldBe("web"),
            assert => assert.Domain.ShouldBe("web.example.com"),
            assert => assert.Upstream.ShouldBe($"web-{deploymentId:N}:80"),
            assert => assert.Route.ShouldBe(TlsRoute.Managed));
    }

    [Fact]
    public async Task Should_Not_Reassert_When_The_Agent_Is_Behind()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v1", runningDeploymentId: Guid.NewGuid());

        // When
        await fixture.DeployEngine.ReassertRoutesAsync("agent-1");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Reassert_When_Never_Deployed_To_The_Agent()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v1");

        // When
        await fixture.DeployEngine.ReassertRoutesAsync("agent-1");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Reassert_While_A_Deployment_Is_In_Progress()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v1", runningDeploymentId: Guid.NewGuid());
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");

        // When
        await fixture.DeployEngine.ReassertRoutesAsync("agent-1");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Mark_The_Deployment_In_Progress_When_Dispatched()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        var result = await fixture.GetDeployments("web");

        // Then
        result.ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.InProgress);
    }

    [Fact]
    public async Task Should_Fail_When_No_Agent_Is_Attached()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        var result = await fixture.GetDeployments("web");

        // Then
        result.ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task Should_Fail_When_An_Attached_Agent_Is_Disconnected()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasAttachment("web", "agent-2");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        var result = await fixture.GetDeployments("web");

        // Then
        result.ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task Should_Not_Dispatch_While_A_Deployment_Is_In_Progress()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v1");
        await fixture.HasPendingDeployment("web", "v2");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        var result = fixture.Agents.GetChannel("agent-1")
            .TryRead(out _);

        // Then
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Roll_Across_Every_Attached_Agent_Oldest_First()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasAttachment("web", "agent-2");
        fixture.Agents.Register("agent-2");
        await fixture.HasPendingDeployment("web", "v1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");
        fixture.Agents.GetChannel("agent-1").TryRead(out var first);
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, null);
        fixture.Agents.GetChannel("agent-2").TryRead(out var second);
        await fixture.DeployEngine.HandleDeployResultAsync("agent-2", deploymentId, healthy: true, null);

        // Then
        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem().Status.ShouldBe(DeploymentStatus.Succeeded);
        (await fixture.FindAttachment("web", "agent-1")).ShouldNotBeNull().RunningTag.ShouldBe("v1");
        (await fixture.FindAttachment("web", "agent-2")).ShouldNotBeNull().RunningTag.ShouldBe("v1");
    }

    [Fact]
    public async Task Should_Stop_And_Fail_When_An_Agent_Reports_Unhealthy()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasAttachment("web", "agent-2");
        fixture.Agents.Register("agent-2");
        await fixture.HasPendingDeployment("web", "v1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, null);
        await fixture.DeployEngine.HandleDeployResultAsync("agent-2", deploymentId, healthy: false, "boom");

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem().Status.ShouldBe(DeploymentStatus.Failed);
        (await fixture.FindAttachment("web", "agent-1")).ShouldNotBeNull().RunningTag.ShouldBe("v1");
        (await fixture.FindAttachment("web", "agent-2")).ShouldNotBeNull().RunningTag.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Advance_The_App_Tag_When_The_Roll_Completes()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, null);

        // Then
        (await fixture.FindApp("web")).ShouldNotBeNull()
            .CurrentImageTag.ShouldBe("v1");
    }

    [Fact]
    public async Task Should_Mark_Failed_On_An_Unhealthy_Result()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: false, "boom");

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task Should_Include_Decrypted_Environment_In_The_Deploy()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.HasEnvVar("web", "API_KEY", "protected:secret");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1")
            .TryRead(out var message);

        // Then
        message.ShouldNotBeNull()
            .Deploy.Env["API_KEY"].ShouldBe("secret");
    }

    [Fact]
    public async Task Should_Carry_A_Bcrypt_Hash_Of_The_Basic_Auth_Password()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", basicAuthUsername: "ops", basicAuthPassword: "protected:secret");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.ShouldSatisfyAllConditions(
            deploy => deploy.BasicAuthUsername.ShouldBe("ops"),
            deploy => deploy.BasicAuthHash.ShouldBe("bcrypt:secret"));
    }

    [Fact]
    public async Task Should_Not_Carry_Basic_Auth_When_The_App_Has_None()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull();
        message.Deploy.BasicAuthUsername.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Attach_Decrypted_Registry_Credentials_When_The_Image_Matches()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", image: "ghcr.io/owner/web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.HasRegistry("ghcr.io", "user", "protected:secret");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1")
            .TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.Registry.ShouldSatisfyAllConditions(
            registry => registry.Host.ShouldBe("ghcr.io"),
            registry => registry.Username.ShouldBe("user"),
            registry => registry.Password.ShouldBe("secret"));
    }

    [Fact]
    public async Task Should_Not_Attach_Registry_Credentials_For_A_Public_Image()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", image: "nginx");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.HasRegistry("ghcr.io", "user", "protected:secret");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1")
            .TryRead(out var message);

        // Then
        message.ShouldNotBeNull()
            .Deploy.Registry.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Render_Volumes_Into_The_Deploy()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.HasVolume("web", "/srv/data", "/data", readOnly: false);
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1")
            .TryRead(out var message);

        // Then
        message.ShouldNotBeNull()
            .Deploy.Compose.ShouldContain("- /srv/data:/data");
    }

    [Fact]
    public async Task Should_Ignore_A_Result_From_An_Agent_It_Is_Not_Waiting_On()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasAttachment("web", "agent-2");
        fixture.Agents.Register("agent-2");
        await fixture.HasPendingDeployment("web", "v1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-2", deploymentId, healthy: true, null);

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem().Status.ShouldBe(DeploymentStatus.InProgress);
        (await fixture.FindAttachment("web", "agent-2")).ShouldNotBeNull().RunningTag.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Fail_An_In_Flight_Deployment_When_Its_Agent_Disconnects()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");

        // When
        await fixture.DeployEngine.HandleAgentDisconnectedAsync("agent-1");

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task Should_Not_Fail_A_Deployment_Waiting_On_A_Different_Agent()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1", "agent-2");

        // When
        await fixture.DeployEngine.HandleAgentDisconnectedAsync("agent-2");

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.InProgress);
    }

    [Fact]
    public async Task Should_Catch_Up_A_Behind_Agent_On_Reconnect()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v1");

        // When
        await fixture.DeployEngine.CatchUpAgentAsync("agent-1");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);
        message.ShouldNotBeNull().Deploy.Tag.ShouldBe("v2");
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.InProgress);
    }

    [Fact]
    public async Task Should_Not_Catch_Up_An_Up_To_Date_Agent()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v2");

        // When
        await fixture.DeployEngine.CatchUpAgentAsync("agent-1");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out _).ShouldBeFalse();
        (await fixture.GetDeployments("web")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Not_Catch_Up_While_A_Deployment_Is_In_Flight()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1", runningTag: "v1");
        await fixture.HasInProgressDeployment("web", "v2", "agent-2");

        // When
        await fixture.DeployEngine.CatchUpAgentAsync("agent-1");

        // Then
        (await fixture.GetDeployments("web")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Not_Catch_Up_When_Nothing_Has_Been_Deployed()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");

        // When
        await fixture.DeployEngine.CatchUpAgentAsync("agent-1");

        // Then
        (await fixture.GetDeployments("web")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Record_The_Previous_Tag_When_The_Tag_Changes()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v2", "agent-1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, null);

        // Then
        (await fixture.FindApp("web")).ShouldNotBeNull().ShouldSatisfyAllConditions(
            app => app.CurrentImageTag.ShouldBe("v2"),
            app => app.PreviousImageTag.ShouldBe("v1"));
    }

    [Fact]
    public async Task Should_Not_Record_A_Previous_Tag_When_Redeploying_The_Same_Tag()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v2");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v2", "agent-1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, null);

        // Then
        (await fixture.FindApp("web")).ShouldNotBeNull().ShouldSatisfyAllConditions(
            app => app.CurrentImageTag.ShouldBe("v2"),
            app => app.PreviousImageTag.ShouldBeNull());
    }

    [Fact]
    public async Task Should_Publish_An_Alert_When_An_Agent_Reports_A_Failed_Deploy()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v2", "agent-1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync(
            "agent-1", deploymentId, healthy: false, "the container exited with code 1");

        // Then
        var evt = fixture.Events.Published.ShouldHaveSingleItem();
        evt.ShouldSatisfyAllConditions(
            a => a.Kind.ShouldBe(AuditEventKind.DeployFailed),
            a => a.Actor.ShouldBe(Actors.System),
            a => a.App.ShouldBe("web"),
            a => a.Agent.ShouldBe("agent-1"),
            a => a.Message.ShouldContain("exited with code 1"));
    }

    [Fact]
    public async Task Should_Publish_An_Alert_When_A_Deploy_Succeeds()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");
        var deploymentId = (await fixture.GetDeployments("web")).Single().Id;

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deploymentId, healthy: true, null);

        // Then
        var evt = fixture.Events.Published.ShouldHaveSingleItem();
        evt.ShouldSatisfyAllConditions(
            a => a.Kind.ShouldBe(AuditEventKind.DeploySucceeded),
            a => a.Actor.ShouldBe(Actors.System),
            a => a.App.ShouldBe("web"),
            a => a.Message.ShouldContain("v1"));
    }

    [Fact]
    public async Task Should_Attribute_A_Deploy_Event_To_The_Triggering_Actor()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", currentImageTag: "v1");
        await fixture.HasAttachment("web", "agent-1");
        var deployment = await fixture.TriggerDeployment("web", "v2", actor: "alice@example.com");

        // When
        await fixture.DeployEngine.HandleDeployResultAsync("agent-1", deployment.Id, healthy: true, null);

        // Then
        var evt = fixture.Events.Published.ShouldHaveSingleItem();
        evt.ShouldSatisfyAllConditions(
            a => a.Kind.ShouldBe(AuditEventKind.DeploySucceeded),
            a => a.Actor.ShouldBe("alice@example.com"));
    }

    [Fact]
    public async Task Should_Refuse_To_Queue_A_Deployment_For_A_Stopped_App()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", stopped: true);
        var app = await fixture.FindApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.DeployEngine.QueueDeploymentAsync(app!, "v1", Actors.System));

        // Then
        exception.ShouldBeOfType<AppStoppedException>();
    }

    [Fact]
    public async Task Should_Deploy_An_Internal_Service_With_No_Route_And_A_Stable_Project()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("postgres", kind: AppKind.Internal, internalPort: 5432);
        await fixture.HasAttachment("postgres", "agent-1");
        await fixture.HasPendingDeployment("postgres", "17");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("postgres");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.ShouldSatisfyAllConditions(
            deploy => deploy.Domain.ShouldBe(string.Empty),
            deploy => deploy.Upstream.ShouldBe(string.Empty),
            deploy => deploy.Project.ShouldBe("luff-postgres"),
            deploy => deploy.Compose.ShouldContain("name: luff-postgres\n"));
    }

    [Fact]
    public async Task Should_Carry_The_Health_Check_In_The_Deploy()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web", appHealthCheckType: AppHealthCheckType.Http, healthCheckEndpoint: "/healthz");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasPendingDeployment("web", "v1");
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // When
        fixture.Agents.GetChannel("agent-1").TryRead(out var message);

        // Then
        message.ShouldNotBeNull().Deploy.ShouldSatisfyAllConditions(
            deploy => deploy.HealthKind.ShouldBe(HealthCheckKind.Http),
            deploy => deploy.HealthTimeoutSeconds.ShouldBe(300),
            deploy => deploy.Compose.ShouldContain("healthcheck:"));
    }

    [Fact]
    public async Task Should_Fail_An_Orphaned_In_Progress_Deployment_On_Startup()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");

        // When
        await fixture.DeployEngine.ReconcileOnStartupAsync();

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task Should_Not_Fail_A_Pending_Deployment_On_Startup()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasPendingDeployment("web", "v1");

        // When
        await fixture.DeployEngine.ReconcileOnStartupAsync();

        // Then
        (await fixture.GetDeployments("web")).ShouldHaveSingleItem()
            .Status.ShouldBe(DeploymentStatus.Pending);
    }

    [Fact]
    public async Task Should_Unwedge_The_Lane_So_A_Deploy_Can_Dispatch_After_Reconcile()
    {
        // Given
        using var fixture = new DeploymentsFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        await fixture.HasInProgressDeployment("web", "v1", "agent-1");
        await fixture.HasPendingDeployment("web", "v2");

        // When
        await fixture.DeployEngine.ReconcileOnStartupAsync();
        await fixture.DeployEngine.TryStartNextDeploymentAsync("web");

        // Then
        fixture.Agents.GetChannel("agent-1").TryRead(out var message).ShouldBeTrue();
        message!.Deploy.Tag.ShouldBe("v2");
    }
}