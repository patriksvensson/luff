using Luff.Protobuf;
using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class UpdateAppHandlerTests
{
    [Fact]
    public async Task Should_Return_The_Updated_App()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.example.com", 80);

        // When
        var result = await fixture.UpdateApp(
            "web", "nginx:alpine", "web.example.com", 8080);

        // Then
        result.ShouldSatisfyAllConditions(
            app => app.Image.ShouldBe("nginx:alpine"),
            app => app.InternalPort.ShouldBe(8080));
    }

    [Fact]
    public async Task Should_Persist_The_Changes()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.example.com", 80);

        // When
        await fixture.UpdateApp(
            "web", "nginx:alpine", "web.example.com", 8080);

        // Then
        (await fixture.GetAppFromDatabase("web"))!.Image.ShouldBe("nginx:alpine");
    }

    [Fact]
    public async Task Should_Throw_When_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.UpdateApp(
                "ghost", "nginx", "ghost.example.com", 80));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }

    [Fact]
    public async Task Should_Reroute_Attached_Agents_When_The_Domain_Changes()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.example.com", 80);
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.UpdateApp(
            "web", "nginx", "new.example.com", 80);

        // Then
        reader.TryRead(out var message).ShouldBeTrue();
        message!.Reroute.ShouldSatisfyAllConditions(
            reroute => reroute.App.ShouldBe("web"),
            reroute => reroute.OldDomain.ShouldBe("web.example.com"),
            reroute => reroute.NewDomain.ShouldBe("new.example.com"));
    }

    [Fact]
    public async Task Should_Not_Reroute_When_The_Domain_Is_Unchanged()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.example.com", 80);
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.UpdateApp(
            "web", "nginx:alpine", "web.example.com", 8080);

        // Then
        reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reroute_When_The_Tls_Mode_Changes_On_A_Real_Domain()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.example.com", 80, TlsMode.Managed);
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.UpdateApp("web", "nginx", "web.example.com", 80, tlsMode: TlsMode.External);

        // Then
        reader.TryRead(out var message).ShouldBeTrue();
        message!.Reroute.ShouldSatisfyAllConditions(
            reroute => reroute.OldDomain.ShouldBe("web.example.com"),
            reroute => reroute.NewDomain.ShouldBe("web.example.com"),
            reroute => reroute.Route.ShouldBe(TlsRoute.External));
    }

    [Fact]
    public async Task Should_Not_Reroute_When_The_Tls_Mode_Changes_On_An_Sslip_Domain()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.127.0.0.1.sslip.io", 80, TlsMode.Managed);
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.UpdateApp("web", "nginx", "web.127.0.0.1.sslip.io", 80, tlsMode: TlsMode.External);

        // Then
        reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reroute_To_Managed_When_The_Domain_Becomes_Real()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web", "nginx", "web.127.0.0.1.sslip.io", 80, TlsMode.Managed);
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.UpdateApp("web", "nginx", "web.example.com", 80);

        // Then
        reader.TryRead(out var message).ShouldBeTrue();
        message!.Reroute.Route.ShouldBe(TlsRoute.Managed);
    }

    [Fact]
    public async Task Should_Update_An_Internal_Service_Without_Rerouting()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasInternalApp("postgres", "postgres", 5432);
        await fixture.HasAttachment("postgres", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        var result = await fixture.UpdateApp(
            new UpdateAppHandler.Request("postgres", "postgres:17", 5432, "operator@example.com"));

        // Then
        result.Image.ShouldBe("postgres:17");
        reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Throw_When_An_Internal_Service_Is_Given_A_Domain()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasInternalApp("postgres", "postgres", 5432);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.UpdateApp(new UpdateAppHandler.Request(
                "postgres", "postgres", 5432, "operator@example.com", domain: "db.example.com")));

        // Then
        exception.ShouldBeOfType<InternalServiceDomainException>();
    }

    [Fact]
    public async Task Should_Publish_An_App_Updated_Event()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.UpdateApp("web", "nginx:2", "web.example.com", 80, actor: "operator@example.com");

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.AppUpdated),
            evt => evt.Actor.ShouldBe("operator@example.com"),
            evt => evt.App.ShouldBe("web"));
    }
}
