using System.Net;
using Luff.Agent.Tests.Fixtures;
using Luff.Protobuf;
using Shouldly;
using Xunit;

namespace Luff.Agent.Tests;

public sealed class CaddyClientTests
{
    [Fact]
    public async Task Should_Replace_The_Front_Door_Config_In_Place_When_It_Already_Exists()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(_ => HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureFrontDoorAsync(
            "cp.example.com", "host.docker.internal:8080",
            managedTls: true, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Patch, "/config/apps/tls"), (HttpMethod.Patch, "/id/luff-frontdoor"),
        });
    }

    [Fact]
    public async Task Should_Create_The_Front_Door_Config_When_There_Is_Nothing_To_Replace()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureFrontDoorAsync(
            "cp.example.com", "host.docker.internal:8080", managedTls: true, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Patch, "/config/apps/tls"), (HttpMethod.Put, "/config/apps/tls"),
            (HttpMethod.Patch, "/id/luff-frontdoor"), (HttpMethod.Post, "/config/apps/http/servers/srv443/routes"),
        });
    }

    [Fact]
    public async Task Should_Pin_A_Default_Sni_When_The_Front_Door_Is_A_Bare_Ip()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(_ => HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureFrontDoorAsync(
            "203.0.113.10", "server:8080", managedTls: false, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Patch, "/config/apps/tls"), (HttpMethod.Patch, "/id/luff-frontdoor"),
            (HttpMethod.Patch, "/config/apps/http/servers/srv443/tls_connection_policies"),
        });
    }

    [Fact]
    public async Task Should_Not_Pin_A_Default_Sni_When_The_Front_Door_Is_A_Domain()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(_ => HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureFrontDoorAsync(
            "cp.example.com", "server:8080", managedTls: true, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Patch, "/config/apps/tls"), (HttpMethod.Patch, "/id/luff-frontdoor"),
        });
    }

    [Fact]
    public async Task Should_Issue_A_Managed_Cert_When_The_Front_Door_Domain_Is_Real()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(_ => HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureFrontDoorAsync(
            "cp.example.com", "server:8080", managedTls: true, CancellationToken.None);

        // Then
        fixture.Handler.Bodies.ShouldContain(body => body != null && body.Contains("acme"));
    }

    [Fact]
    public async Task Should_Issue_A_Self_Signed_Cert_When_The_Front_Door_Domain_Is_Not_Real()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(_ => HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureFrontDoorAsync(
            "127.0.0.1.sslip.io", "server:8080", managedTls: false, CancellationToken.None);

        // Then
        fixture.Handler.Bodies.ShouldContain(body => body != null && body.Contains("internal"));
    }

    [Fact]
    public async Task Should_Create_A_Managed_Route_On_The_443_Server()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureRouteAsync("web.example.com", "web-d1:80", TlsRoute.Managed,
            basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Patch, "/id/luff-web.example.com-proxy/upstreams"),
            (HttpMethod.Post, "/config/apps/http/servers/srv443/routes"),
        });
    }

    [Fact]
    public async Task Should_Create_An_Http_Route_On_The_80_Server()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureRouteAsync("web.127.0.0.1.sslip.io", "web-d1:80", TlsRoute.Http,
            basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Patch, "/id/luff-web.127.0.0.1.sslip.io-proxy/upstreams"),
            (HttpMethod.Post, "/config/apps/http/servers/srv0/routes"),
        });
    }

    [Fact]
    public async Task Should_Force_Https_On_An_External_Route()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureRouteAsync(
            "app.example.com", "app-d1:80",
            TlsRoute.External, basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldContain((HttpMethod.Post, "/config/apps/http/servers/srv0/routes"));
        fixture.Handler.Bodies.ShouldContain(body =>
            body != null && body.Contains("X-Forwarded-Proto") && body.Contains("https"));
    }

    [Fact]
    public async Task Should_Not_Force_Https_On_A_Plain_Http_Route()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureRouteAsync(
            "web.127.0.0.1.sslip.io", "web-d1:80",
            TlsRoute.Http,
            basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Bodies.ShouldAllBe(body => body == null || !body.Contains("X-Forwarded-Proto"));
    }

    [Fact]
    public async Task Should_Create_The_New_Route_Before_Removing_The_Old_When_The_Host_Changes()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(
            request => request.Method == HttpMethod.Patch
                       && request.RequestUri!.AbsolutePath.EndsWith("/upstreams", StringComparison.Ordinal)
                ? HttpStatusCode.NotFound
                : HttpStatusCode.OK,
            request => request.Method == HttpMethod.Get ? "[{\"dial\":\"web-d1:80\"}]" : null);

        // When
        await fixture.Client.RerouteAsync("old.example.com", "new.example.com", TlsRoute.Managed,
            basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Get, "/id/luff-old.example.com-proxy/upstreams"),
            (HttpMethod.Patch, "/id/luff-new.example.com-proxy/upstreams"),
            (HttpMethod.Post, "/config/apps/http/servers/srv443/routes"),
            (HttpMethod.Delete, "/id/luff-old.example.com"),
        });
    }

    [Fact]
    public async Task Should_Remove_Before_Recreating_When_A_Mode_Change_Keeps_The_Host()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(
            request => request.Method == HttpMethod.Patch
                       && request.RequestUri!.AbsolutePath.EndsWith("/upstreams", StringComparison.Ordinal)
                ? HttpStatusCode.NotFound
                : HttpStatusCode.OK,
            request => request.Method == HttpMethod.Get ? "[{\"dial\":\"web-d1:80\"}]" : null);

        // When
        await fixture.Client.RerouteAsync(
            "web.example.com", "web.example.com", TlsRoute.Http, basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Requests.ShouldBe(new[]
        {
            (HttpMethod.Get, "/id/luff-web.example.com-proxy/upstreams"),
            (HttpMethod.Delete, "/id/luff-web.example.com"),
            (HttpMethod.Patch, "/id/luff-web.example.com-proxy/upstreams"),
            (HttpMethod.Post, "/config/apps/http/servers/srv0/routes"),
        });
    }

    [Fact]
    public async Task Should_Gate_A_Route_With_A_Basic_Auth_Handler_Ahead_Of_The_Proxy()
    {
        // Given
        const string Hash = "$2a$11$abcdefghijklmnopqrstuOeH1sMLViJ8p8Iq6R5xq7Qv9c0m1n2O";
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureRouteAsync(
            "web.example.com", "web-d1:80", TlsRoute.Managed, new BasicAuth("ops", Hash), CancellationToken.None);

        // Then
        var body = fixture.Handler.Bodies.SingleOrDefault(body =>
            body?.Contains("reverse_proxy", StringComparison.Ordinal) == true);

        body.ShouldNotBeNull();
        body.ShouldContain("http_basic");
        body.ShouldContain("\"ops\"");
        body.ShouldContain(Hash);
        body.IndexOf("authentication", StringComparison.Ordinal)
            .ShouldBeLessThan(body.IndexOf("reverse_proxy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_Not_Add_A_Basic_Auth_Handler_When_There_Are_No_Credentials()
    {
        // Given
        var fixture = new CaddyAdminClientFixture(request =>
            request.Method == HttpMethod.Patch ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // When
        await fixture.Client.ConfigureRouteAsync(
            "web.example.com", "web-d1:80", TlsRoute.Managed, basicAuth: null, CancellationToken.None);

        // Then
        fixture.Handler.Bodies.ShouldAllBe(body => body == null || !body.Contains("http_basic"));
    }
}