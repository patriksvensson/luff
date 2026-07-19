using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class SetBasicAuthHandlerTests
{
    [Fact]
    public async Task Should_Store_The_Username_And_The_Protected_Password()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        await fixture.SetBasicAuth("web", "admin", "s3cret");

        // Then
        var app = await fixture.GetAppFromDatabase("web");
        app!.ShouldSatisfyAllConditions(
            stored => stored.BasicAuthUsername.ShouldBe("admin"),
            stored => stored.BasicAuthPassword.ShouldBe("protected:s3cret"));
    }

    [Fact]
    public async Task Should_Trim_The_Username()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        await fixture.SetBasicAuth("web", "  admin  ", "s3cret");

        // Then
        (await fixture.GetAppFromDatabase("web"))!.BasicAuthUsername.ShouldBe("admin");
    }

    [Fact]
    public async Task Should_Reroute_Attached_Agents_With_The_Gate()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.SetBasicAuth("web", "admin", "s3cret");

        // Then
        reader.TryRead(out var message).ShouldBeTrue();
        message!.Reroute.ShouldSatisfyAllConditions(
            reroute => reroute.App.ShouldBe("web"),
            reroute => reroute.OldDomain.ShouldBe("web.example.com"),
            reroute => reroute.NewDomain.ShouldBe("web.example.com"),
            reroute => reroute.BasicAuthUsername.ShouldBe("admin"),
            reroute => reroute.BasicAuthHash.ShouldBe("bcrypt:s3cret"));
    }

    [Fact]
    public async Task Should_Throw_For_A_Frontless_App()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasInternalApp("postgres", "postgres", 5432);

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.SetBasicAuth("postgres", "admin", "s3cret"));

        // Then
        exception.ShouldBeOfType<BasicAuthNotSupportedException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Username_Is_Empty()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.SetBasicAuth("web", "   ", "s3cret"));

        // Then
        exception.ShouldBeOfType<InvalidBasicAuthException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Password_Is_Empty()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.SetBasicAuth("web", "admin", ""));

        // Then
        exception.ShouldBeOfType<InvalidBasicAuthException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Username_Contains_A_Colon()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.SetBasicAuth("web", "ad:min", "s3cret"));

        // Then
        exception.ShouldBeOfType<InvalidBasicAuthException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.SetBasicAuth("ghost", "admin", "s3cret"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }

    [Fact]
    public async Task Should_Publish_An_App_Updated_Event()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        await fixture.SetBasicAuth("web", "admin", "s3cret", actor: "operator@example.com");

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.AppUpdated),
            evt => evt.Actor.ShouldBe("operator@example.com"),
            evt => evt.App.ShouldBe("web"));
    }
}
