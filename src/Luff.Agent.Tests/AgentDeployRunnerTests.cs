using Luff.Agent.Tests.Fixtures;
using Luff.Protobuf;
using Shouldly;
using Xunit;

namespace Luff.Agent.Tests;

public sealed class AgentDeployRunnerTests
{
    [Fact]
    public async Task Should_Report_Healthy_When_The_Deploy_Succeeds()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        result.Healthy.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Report_Unhealthy_With_The_Output_When_Compose_Fails()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForFailure("boom");

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        result.ShouldSatisfyAllConditions(
            reported => reported.Healthy.ShouldBeFalse(),
            reported => reported.FailureReason.ShouldBe("boom"));
    }

    [Fact]
    public async Task Should_Route_The_Domain_To_Green()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        fixture.Caddy.Host.ShouldBe("web.example.com");
        fixture.Caddy.Upstream.ShouldBe("web-d1:80");
    }

    [Fact]
    public async Task Should_Not_Route_An_Internal_Service_With_No_Domain()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "postgres",
                Tag = "17",
                Compose = "name: luff-postgres",
                Domain = string.Empty,
                InternalPort = 5432,
                Project = "luff-postgres",
            },
            CancellationToken.None);

        // Then
        result.Healthy.ShouldBeTrue();
        fixture.Caddy.Host.ShouldBeNull();
        fixture.Phases.ShouldNotContain("swapping");
    }

    [Fact]
    public async Task Should_Not_Remove_A_Route_For_An_Internal_Service_On_Detach()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RemoveAsync("postgres", string.Empty);

        // Then
        fixture.Caddy.RemovedHost.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Fail_When_The_Container_Crash_Loops()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();
        fixture.DockerCompose.InspectResult = new ContainerStatus(
            Running: true, Restarting: true, RestartCount: 3, ExitCode: 1, Health: null);
        fixture.DockerCompose.TailedLogs = "FATAL: password authentication failed";

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "postgres",
                Tag = "17",
                Compose = "name: luff-postgres",
                Domain = string.Empty,
                InternalPort = 5432,
                Project = "luff-postgres",
            },
            CancellationToken.None);

        // Then
        result.Healthy.ShouldBeFalse();
        result.FailureReason.ShouldContain("restart-looping");
        result.FailureReason.ShouldContain("password authentication failed");
        fixture.Phases.ShouldNotContain("swapping");
    }

    [Fact]
    public async Task Should_Fail_Fast_When_A_Tcp_Service_Crashes_Instead_Of_Waiting_Out_The_Timeout()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();
        fixture.TcpProbe.Connects = false;
        fixture.DockerCompose.InspectResult = new ContainerStatus(
            Running: false, Restarting: false, RestartCount: 0, ExitCode: 1, Health: null);
        fixture.DockerCompose.TailedLogs = "You must specify POSTGRES_PASSWORD";

        // When — a long timeout that we never advance; a fail-fast gate must not need it.
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "postgres",
                Tag = "17",
                Compose = "name: luff-postgres",
                Domain = string.Empty,
                InternalPort = 5432,
                Project = "luff-postgres",
                HealthKind = HealthCheckKind.Tcp,
                HealthTimeoutSeconds = 300,
            },
            CancellationToken.None);

        // Then
        result.Healthy.ShouldBeFalse();
        result.FailureReason.ShouldContain("exited with code 1");
        result.FailureReason.ShouldContain("POSTGRES_PASSWORD");
    }

    [Fact]
    public async Task Should_Probe_Tcp_Readiness_For_A_Tcp_Health_Check()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "postgres",
                Tag = "17",
                Compose = "name: luff-postgres",
                Domain = string.Empty,
                InternalPort = 5432,
                Project = "luff-postgres",
                HealthKind = HealthCheckKind.Tcp,
                HealthTimeoutSeconds = 30,
            },
            CancellationToken.None);

        // Then
        result.Healthy.ShouldBeTrue();
        fixture.TcpProbe.Host.ShouldBe("postgres");
        fixture.TcpProbe.Port.ShouldBe(5432);
    }

    [Fact]
    public async Task Should_Fail_When_Tcp_Readiness_Never_Succeeds()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();
        fixture.TcpProbe.Connects = false;

        // When
        var task = fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "postgres",
                Tag = "17",
                Compose = "name: luff-postgres",
                Domain = string.Empty,
                InternalPort = 5432,
                Project = "luff-postgres",
                HealthKind = HealthCheckKind.Tcp,
                HealthTimeoutSeconds = 10,
            },
            CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        }

        var result = await task;

        // Then
        result.Healthy.ShouldBeFalse();
        result.FailureReason.ShouldContain("did not accept a TCP connection");
    }

    [Fact]
    public async Task Should_Stop_And_Start_Containers_By_App()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.StopAppAsync("postgres");
        await fixture.StartAppAsync("postgres");

        // Then
        fixture.DockerCompose.StoppedApp.ShouldBe("postgres");
        fixture.DockerCompose.StartedApp.ShouldBe("postgres");
    }

    [Fact]
    public async Task Should_Not_Route_When_Compose_Fails()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForFailure("boom");

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        fixture.Caddy.Host.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Prune_Old_Containers_Keeping_Green()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        fixture.DockerCompose.PrunedApp.ShouldBe("web");
        fixture.DockerCompose.KeptProject.ShouldBe("luff-web-d1");
    }

    [Fact]
    public async Task Should_Pass_The_Environment_To_Compose()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
                Env = { { "API_KEY", "secret" } },
            },
            CancellationToken.None);

        // Then
        fixture.DockerCompose.Environment.ShouldNotBeNull()
            ["API_KEY"].ShouldBe("secret");
    }

    [Fact]
    public async Task Should_Authenticate_When_Registry_Credentials_Are_Present()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
                Registry = new RegistryCredentials
                {
                    Host = "ghcr.io",
                    Username = "user",
                    Password = "secret",
                },
            },
            CancellationToken.None);

        // Then
        fixture.DockerCompose.LoginHost.ShouldBe("ghcr.io");
        fixture.DockerCompose.LoginUsername.ShouldBe("user");
        fixture.DockerCompose.LoginPassword.ShouldBe("secret");
    }

    [Fact]
    public async Task Should_Not_Authenticate_Without_Registry_Credentials()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        fixture.DockerCompose.LoginHost.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Remove_Containers_And_Route_On_Detach()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RemoveAsync(
            "web", "web.example.com",
            CancellationToken.None);

        // Then
        fixture.DockerCompose.RemovedApp.ShouldBe("web");
        fixture.Caddy.RemovedHost.ShouldBe("web.example.com");
    }

    [Fact]
    public async Task Should_Pass_The_Wait_Timeout_For_A_Docker_Health_Check()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
                HealthKind = HealthCheckKind.Docker,
                HealthTimeoutSeconds = 120,
            },
            CancellationToken.None);

        // Then
        fixture.DockerCompose.UpWaitTimeoutSeconds.ShouldBe(120);
    }

    [Fact]
    public async Task Should_Skip_Wait_And_Delay_For_A_None_Health_Check()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        var task = fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
                HealthKind = HealthCheckKind.None,
                HealthTimeoutSeconds = 5,
            },
            CancellationToken.None);

        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await task;

        // Then
        fixture.DockerCompose.UpWaitTimeoutSeconds.ShouldBeNull();
        fixture.Caddy.Host.ShouldBe("web.example.com");
    }

    [Fact]
    public async Task Should_Reroute_The_Domain_On_The_Caddy_Client()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RerouteAsync("old.example.com", "new.example.com", TlsRoute.Managed);

        // Then
        fixture.Caddy.ShouldSatisfyAllConditions(
            caddy => caddy.RerouteOldHost.ShouldBe("old.example.com"),
            caddy => caddy.RerouteNewHost.ShouldBe("new.example.com"),
            caddy => caddy.RerouteKind.ShouldBe(TlsRoute.Managed));
    }

    [Fact]
    public async Task Should_Remove_Then_Recreate_The_Route_On_Assert()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.AssertRouteAsync("web.example.com", "web-d1:80", TlsRoute.Managed);

        // Then
        fixture.Caddy.ShouldSatisfyAllConditions(
            caddy => caddy.RemovedHost.ShouldBe("web.example.com"),
            caddy => caddy.Host.ShouldBe("web.example.com"),
            caddy => caddy.Upstream.ShouldBe("web-d1:80"),
            caddy => caddy.RouteKind.ShouldBe(TlsRoute.Managed));
    }

    [Fact]
    public async Task Should_Route_A_Managed_App_On_The_443_Server()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(new Deploy
        {
            DeploymentId = "d1",
            App = "web",
            Tag = "v1",
            Compose = "name: luff-web-d1",
            Domain = "web.example.com",
            InternalPort = 80,
            Upstream = "web-d1:80",
            Project = "luff-web-d1",
            TlsRoute = TlsRoute.Managed,
        });

        // Then
        fixture.Caddy.RouteKind.ShouldBe(TlsRoute.Managed);
    }

    [Fact]
    public async Task Should_Configure_The_Front_Door_On_The_Caddy_Client()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.ConfigureFrontDoorAsync(
            "cp.example.com", "host.docker.internal:8080", CancellationToken.None);

        // Then
        fixture.Caddy.FrontDoorDomain.ShouldBe("cp.example.com");
        fixture.Caddy.FrontDoorUpstream.ShouldBe("host.docker.internal:8080");
    }

    [Fact]
    public async Task Should_Fail_Without_Starting_A_Non_Conformant_Compose()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForFailure("boom");

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = """
                    name: luff-web-d1
                    services:
                      app:
                        image: "nginx:v1"
                        privileged: true
                    """,
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        fixture.DockerCompose.Yaml.ShouldBeNull();
        fixture.Caddy.Host.ShouldBeNull();
        result.Healthy.ShouldBeFalse();
        result.FailureReason.ShouldBe(
            "Compose validation failed: The compose service " +
            "contains an unexpected key 'privileged'");
    }

    [Fact]
    public async Task Should_Report_Phases_In_Order_On_Success()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();

        // When
        await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        fixture.Phases.ShouldBe(["pulling", "starting", "healthy", "swapping", "draining"]);
        fixture.DockerCompose.Pulled.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Stop_At_Pulling_When_The_Pull_Fails()
    {
        // Given
        var fixture = DeployRunnerFixture.CreateForSuccess();
        fixture.DockerCompose.PullResult = new DockerComposeResult(false, "no such image");

        // When
        var result = await fixture.RunAsync(
            new Deploy
            {
                DeploymentId = "d1",
                App = "web",
                Tag = "v1",
                Compose = "name: luff-web-d1",
                Domain = "web.example.com",
                InternalPort = 80,
                Upstream = "web-d1:80",
                Project = "luff-web-d1",
            },
            CancellationToken.None);

        // Then
        result.Healthy.ShouldBeFalse();
        result.FailureReason.ShouldBe("no such image");
        fixture.Phases.ShouldBe(["pulling"]);
    }
}