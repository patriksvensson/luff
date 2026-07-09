using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Deployments;

public sealed class DockerComposeRendererTests
{
    [Fact]
    public void Should_Render_An_Internal_Service_With_A_Stable_Project_And_Bare_Alias()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "postgres",
            Kind = AppKind.Internal,
            Image = "postgres",
            Domain = null,
            InternalPort = 5432,
        };

        // When
        var result = renderer.Render(app, Guid.NewGuid(), "17", [], [], []);

        // Then
        result.ShouldSatisfyAllConditions(
            r => r.ShouldContain("name: luff-postgres\n"),
            r => r.ShouldContain("- \"postgres\""),
            r => r.ShouldNotContain("healthcheck:"));
    }

    [Fact]
    public void Should_Render_The_App_As_A_Compose_Project()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "web",
            Image = "nginx",
            Domain = "web.example.com",
            InternalPort = 80,
        };

        // When
        var result = renderer.Render(
            app, Guid.Empty, "v1", [], [], []);

        // Then
        result.ShouldBe(
            """
            name: luff-web-00000000000000000000000000000000
            services:
              app:
                image: "nginx:v1"
                labels:
                  luff.managed: "true"
                  luff.app: "web"
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - "web-00000000000000000000000000000000"
            networks:
              luff:
                external: true
            """
        );
    }

    [Fact]
    public void Should_Declare_Environment_Keys_Sorted()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "web",
            Image = "nginx",
            Domain = "web.example.com",
            InternalPort = 80,
        };

        // When
        var result = renderer.Render(
            app, Guid.Empty, "v1",
            ["DATABASE_URL", "API_KEY"], [], []);

        // Then
        result.ShouldBe(
            """
            name: luff-web-00000000000000000000000000000000
            services:
              app:
                image: "nginx:v1"
                environment:
                  - API_KEY
                  - DATABASE_URL
                labels:
                  luff.managed: "true"
                  luff.app: "web"
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - "web-00000000000000000000000000000000"
            networks:
              luff:
                external: true
            """);
    }

    [Fact]
    public void Should_Render_Bind_And_Named_Volumes()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "web",
            Image = "nginx",
            Domain = "web.example.com",
            InternalPort = 80,
        };

        // When
        var result = renderer.Render(
            app, Guid.Empty, "v1", [],
            [
                new Volume { AppName = "web", Source = "/srv/data", Target = "/data", ReadOnly = false },
                new Volume { AppName = "web", Source = "cache", Target = "/var/cache", ReadOnly = true },
            ], []);

        // Then
        result.ShouldBe(
            """
            name: luff-web-00000000000000000000000000000000
            services:
              app:
                image: "nginx:v1"
                volumes:
                  - "/srv/data:/data"
                  - "cache:/var/cache:ro"
                labels:
                  luff.managed: "true"
                  luff.app: "web"
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - "web-00000000000000000000000000000000"
            networks:
              luff:
                external: true
            volumes:
              cache:
                name: luff-web-cache
            """);
    }

    [Fact]
    public void Should_Render_An_Http_Healthcheck_Block()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "web",
            Image = "nginx",
            Domain = "web.example.com",
            InternalPort = 80,
            HealthCheckType = AppHealthCheckType.Http,
            HealthCheckEndpoint = "/healthz",
            HealthCheckTimeoutSeconds = 120,
        };

        // When
        var result = renderer.Render(app, Guid.Empty, "v1", [], [], []);

        // Then
        result.ShouldBe(
            """
            name: luff-web-00000000000000000000000000000000
            services:
              app:
                image: "nginx:v1"
                healthcheck:
                  test: ["CMD-SHELL", "wget -q --spider http://localhost:80/healthz 2>/dev/null || curl -fsS http://localhost:80/healthz"]
                  interval: 5s
                  timeout: 3s
                  retries: 3
                  start_period: 120s
                labels:
                  luff.managed: "true"
                  luff.app: "web"
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - "web-00000000000000000000000000000000"
            networks:
              luff:
                external: true
            """);
    }

    [Fact]
    public void Should_Not_Render_A_Healthcheck_For_The_Docker_Type()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "web",
            Image = "nginx",
            Domain = "web.example.com",
            InternalPort = 80,
            HealthCheckType = AppHealthCheckType.Docker,
        };

        // When
        var result = renderer.Render(app, Guid.Empty, "v1", [], [], []);

        // Then
        result.ShouldBe(
            """
            name: luff-web-00000000000000000000000000000000
            services:
              app:
                image: "nginx:v1"
                labels:
                  luff.managed: "true"
                  luff.app: "web"
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - "web-00000000000000000000000000000000"
            networks:
              luff:
                external: true
            """ );
    }

    [Fact]
    public void Should_Render_A_Direct_App_With_Loopback_Published_Ports()
    {
        // Given
        var renderer = new DockerComposeRenderer();
        var app = new App
        {
            Name = "tool",
            Kind = AppKind.Direct,
            Image = "grafana",
            Domain = null,
            InternalPort = 3000,
        };

        // When
        var result = renderer.Render(
            app, Guid.NewGuid(), "11", [], [],
            [
                new PortMapping { AppName = "tool", HostPort = 8002, ContainerPort = 9090 },
                new PortMapping { AppName = "tool", HostPort = 8001, ContainerPort = 3000 },
            ]);

        // Then
        result.ShouldBe(
            """
            name: luff-tool
            services:
              app:
                image: "grafana:11"
                ports:
                  - "127.0.0.1:8001:3000"
                  - "127.0.0.1:8002:9090"
                labels:
                  luff.managed: "true"
                  luff.app: "tool"
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - "tool"
            networks:
              luff:
                external: true
            """);
    }
}