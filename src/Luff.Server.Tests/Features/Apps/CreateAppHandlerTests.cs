using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class CreateAppHandlerTests
{
    [Fact]
    public async Task Should_Return_Created_App()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.CreateApp(
            "web", "nginx", "web.example.com", 80);

        // Then
        result.ShouldSatisfyAllConditions(
            app => app.Name.ShouldBe("web"),
            app => app.Image.ShouldBe("nginx"),
            app => app.Domain.ShouldBe("web.example.com"),
            app => app.InternalPort.ShouldBe(80));
    }

    [Fact]
    public async Task Should_Persist_The_App()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        await fixture.CreateApp(
            "web", "nginx", "web.example.com", 80);

        // Then
        (await fixture.GetAppFromDatabase("web")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Throw_When_App_Already_Exists()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateApp("web", "nginx", "web.example.com", 80));

        // Then
        exception.ShouldBeOfType<AppAlreadyExistsException>();
    }

    [Fact]
    public async Task Should_Default_To_Managed_Tls()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.CreateApp("web", "nginx", "web.example.com", 80);

        // Then
        result.TlsMode.ShouldBe(TlsMode.Managed);
    }

    [Fact]
    public async Task Should_Persist_An_Explicit_External_Tls_Mode()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.CreateApp("web", "nginx", "web.example.com", 80, tlsMode: TlsMode.External);

        // Then
        result.TlsMode.ShouldBe(TlsMode.External);
    }

    [Fact]
    public async Task Should_Default_To_A_Web_App()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.CreateApp("web", "nginx", "web.example.com", 80);

        // Then
        result.Kind.ShouldBe(AppKind.Web);
    }

    [Fact]
    public async Task Should_Throw_When_A_Web_App_Has_No_Domain()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateApp(new CreateAppHandler.Request("web", "nginx", 80, kind: AppKind.Web, domain: null)));

        // Then
        exception.ShouldBeOfType<InvalidDomainException>();
    }

    [Fact]
    public async Task Should_Create_An_Internal_Service_Without_A_Domain()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.CreateApp(
            new CreateAppHandler.Request("postgres", "postgres", 5432, kind: AppKind.Internal));

        // Then
        result.ShouldSatisfyAllConditions(
            app => app.Kind.ShouldBe(AppKind.Internal),
            app => app.Domain.ShouldBeNull(),
            app => app.InternalPort.ShouldBe(5432),
            app => app.HealthCheck.Type.ShouldBe(AppHealthCheckType.Tcp));
    }

    [Fact]
    public async Task Should_Throw_When_An_Internal_Service_Is_Given_A_Domain()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateApp(new CreateAppHandler.Request(
                "postgres", "postgres", 5432, kind: AppKind.Internal, domain: "db.example.com")));

        // Then
        exception.ShouldBeOfType<InternalServiceDomainException>();
    }

    [Theory]
    [InlineData("server")]
    [InlineData("agent")]
    [InlineData("caddy")]
    public async Task Should_Throw_When_An_Internal_Service_Uses_A_Reserved_Name(string name)
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateApp(new CreateAppHandler.Request(name, "postgres", 5432, kind: AppKind.Internal)));

        // Then
        exception.ShouldBeOfType<ReservedServiceNameException>();
    }

    [Fact]
    public async Task Should_Create_A_Direct_App_Without_A_Domain()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var result = await fixture.CreateApp(
            new CreateAppHandler.Request("tool", "grafana", 3000, kind: AppKind.Direct));

        // Then
        result.ShouldSatisfyAllConditions(
            app => app.Kind.ShouldBe(AppKind.Direct),
            app => app.Domain.ShouldBeNull(),
            app => app.InternalPort.ShouldBe(3000),
            app => app.HealthCheck.Type.ShouldBe(AppHealthCheckType.Tcp));
    }

    [Fact]
    public async Task Should_Throw_When_A_Direct_App_Is_Given_A_Domain()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateApp(new CreateAppHandler.Request(
                "tool", "grafana", 3000, kind: AppKind.Direct, domain: "tool.example.com")));

        // Then
        exception.ShouldBeOfType<DirectAppDomainException>();
    }

    [Theory]
    [InlineData("server")]
    [InlineData("agent")]
    [InlineData("caddy")]
    public async Task Should_Throw_When_A_Direct_App_Uses_A_Reserved_Name(string name)
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateApp(new CreateAppHandler.Request(name, "grafana", 3000, kind: AppKind.Direct)));

        // Then
        exception.ShouldBeOfType<ReservedServiceNameException>();
    }
}
