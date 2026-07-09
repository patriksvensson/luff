using Shouldly;
using Xunit;

namespace Luff.Agent.Tests;

public sealed class DockerComposeValidatorTests
{
    [Fact]
    public void Should_Accept_A_Minimal_Rendered_Project()
    {
        // Given
        var compose =
            """
            name: luff-web-d1
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
                      - "web-d1"
            networks:
              luff:
                external: true
            """;

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldBeNull();
    }

    [Fact]
    public void Should_Accept_A_Full_Featured_Rendered_Project()
    {
        // Given
        var compose =
            """
            name: luff-web-00000000000000000000000000000000
            services:
              app:
                image: "nginx:v1"
                environment:
                  - API_KEY
                  - DATABASE_URL
                volumes:
                  - "/srv/data:/data"
                  - "cache:/var/cache:ro"
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
            volumes:
              cache:
                name: luff-web-cache
            """;

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldBeNull();
    }

    [Fact]
    public void Should_Reject_A_Privileged_Service()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                privileged: true
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("privileged");
    }

    [Fact]
    public void Should_Reject_Host_Networking()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                network_mode: host
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("network_mode");
    }

    [Fact]
    public void Should_Reject_Added_Capabilities()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                cap_add:
                  - SYS_ADMIN
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("cap_add");
    }

    [Fact]
    public void Should_Reject_A_Docker_Socket_Bind_Mount()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                volumes:
                  - "/var/run/docker.sock:/var/run/docker.sock"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("/var/run/docker.sock");
    }

    [Fact]
    public void Should_Reject_A_Root_Bind_Mount()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                volumes:
                  - "/:/host"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("not an allowed host path");
    }

    [Fact]
    public void Should_Reject_A_Denied_Root_Bind_Mount()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                volumes:
                  - "/etc:/etc"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("not an allowed host path");
    }

    [Fact]
    public void Should_Reject_A_Traversal_Bind_Mount()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                volumes:
                  - "/srv/../etc:/x"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("..");
    }

    [Fact]
    public void Should_Reject_A_Long_Form_Volume()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                volumes:
                  - type: bind
                    source: /var/run/docker.sock
                    target: /sock
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("'source:target' string");
    }

    [Fact]
    public void Should_Reject_A_Named_Volume_With_A_Bind_Device()
    {
        // Given
        var compose =
            """
            name: luff-web-d1
            services:
              app:
                image: "nginx:v1"
                volumes:
                  - "evil:/data"
                labels:
                  luff.managed: "true"
                restart: unless-stopped
            volumes:
              evil:
                driver: local
                driver_opts:
                  type: none
                  o: bind
                  device: /var/run/docker.sock
            """;

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("driver");
    }

    [Fact]
    public void Should_Reject_An_External_Network_Driver_Key()
    {
        // Given
        var compose =
            """
            name: luff-web-d1
            services:
              app:
                image: "nginx:v1"
                restart: unless-stopped
            networks:
              luff:
                driver: macvlan
            """;

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("network declaration");
    }

    [Fact]
    public void Should_Reject_An_Unexpected_Top_Level_Key()
    {
        // Given
        var compose =
            """
            name: luff-web-d1
            services:
              app:
                image: "nginx:v1"
                restart: unless-stopped
            x-secret: anything
            """;

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("unexpected top-level key");
    }

    [Fact]
    public void Should_Reject_A_Merge_Key_Injection()
    {
        // Given
        var compose =
            """
            name: luff-web-d1
            services:
              app:
                <<: &escape
                  privileged: true
                image: "nginx:v1"
                restart: unless-stopped
            """;

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Should_Reject_Malformed_Yaml()
    {
        // Given
        var compose = "name: [unterminated";

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("could not be parsed");
    }

    [Fact]
    public void Should_Accept_Loopback_Published_Ports()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                ports:
                  - "127.0.0.1:8001:3000"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldBeNull();
    }

    [Fact]
    public void Should_Reject_A_Non_Loopback_Published_Port()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                ports:
                  - "0.0.0.0:8001:3000"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("loopback");
    }

    [Fact]
    public void Should_Reject_A_Bare_Published_Port()
    {
        // Given
        var compose = Service("""
                image: "nginx:v1"
                ports:
                  - "8001:3000"
            """);

        // When
        var result = DockerComposeValidator.Validate(compose);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("loopback");
    }

    private static string Service(string serviceBody)
    {
        return
            $"""
            name: luff-web-d1
            services:
              app:
            {serviceBody}
            """;
    }
}
