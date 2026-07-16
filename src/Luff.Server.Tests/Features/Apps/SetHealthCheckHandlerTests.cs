using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class SetHealthCheckHandlerTests
{
    [Fact]
    public async Task Should_Persist_An_Http_Health_Check()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.SetHealthCheck("web", AppHealthCheckType.Http, "/healthz", 120);

        // Then
        (await fixture.GetAppFromDatabase("web")).ShouldNotBeNull().ShouldSatisfyAllConditions(
            app => app.HealthCheckType.ShouldBe(AppHealthCheckType.Http),
            app => app.HealthCheckEndpoint.ShouldBe("/healthz"),
            app => app.HealthCheckTimeoutSeconds.ShouldBe(120));
    }

    [Fact]
    public async Task Should_Default_The_Endpoint_To_Root_For_Http()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.SetHealthCheck("web", AppHealthCheckType.Http, null, 30);

        // Then
        (await fixture.GetAppFromDatabase("web")).ShouldNotBeNull()
            .HealthCheckEndpoint.ShouldBe("/");
    }

    [Fact]
    public async Task Should_Ignore_The_Endpoint_For_A_None_Health_Check()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        await fixture.SetHealthCheck("web", AppHealthCheckType.None, "/ignored", 30);

        // Then
        (await fixture.GetAppFromDatabase("web")).ShouldNotBeNull().ShouldSatisfyAllConditions(
            app => app.HealthCheckType.ShouldBe(AppHealthCheckType.None),
            app => app.HealthCheckEndpoint.ShouldBeNull());
    }

    [Fact]
    public async Task Should_Reject_An_Unsafe_Endpoint()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.SetHealthCheck("web", AppHealthCheckType.Http, "/health; rm -rf /", 30));

        // Then
        exception.ShouldBeOfType<InvalidHealthCheckException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.SetHealthCheck("ghost", AppHealthCheckType.Docker, null, 30));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }

    [Fact]
    public async Task Should_Throw_On_An_Http_Health_Check_For_An_Internal_Service()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasInternalApp("postgres", "postgres", 5432);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.SetHealthCheck("postgres", AppHealthCheckType.Http, "/health", 30));

        // Then
        exception.ShouldBeOfType<InvalidHealthCheckException>();
    }

    [Fact]
    public async Task Should_Throw_On_An_Http_Health_Check_For_A_Direct_App()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp("tool", domain: null, kind: AppKind.Direct);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.SetHealthCheck("tool", AppHealthCheckType.Http, "/health", 30));

        // Then
        exception.ShouldBeOfType<InvalidHealthCheckException>();
    }
}
