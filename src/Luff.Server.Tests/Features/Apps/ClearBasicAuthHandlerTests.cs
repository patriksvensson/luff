using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class ClearBasicAuthHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Stored_Credentials()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();
        await fixture.SetBasicAuth("web", "admin", "s3cret");

        // When
        await fixture.ClearBasicAuth("web");

        // Then
        var app = await fixture.GetAppFromDatabase("web");
        app!.ShouldSatisfyAllConditions(
            stored => stored.BasicAuthUsername.ShouldBeNull(),
            stored => stored.BasicAuthPassword.ShouldBeNull());
    }

    [Fact]
    public async Task Should_Reroute_Attached_Agents_Without_The_Gate()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();
        await fixture.SetBasicAuth("web", "admin", "s3cret");
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.ClearBasicAuth("web");

        // Then
        reader.TryRead(out var message).ShouldBeTrue();
        message.Reroute.ShouldSatisfyAllConditions(
            reroute => reroute.App.ShouldBe("web"),
            reroute => reroute.OldDomain.ShouldBe("web.example.com"),
            reroute => reroute.NewDomain.ShouldBe("web.example.com"),
            reroute => reroute.BasicAuthUsername.ShouldBe(string.Empty),
            reroute => reroute.BasicAuthHash.ShouldBe(string.Empty));
    }

    [Fact]
    public async Task Should_Not_Reroute_When_Nothing_Is_Configured()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();
        await fixture.HasAttachment("web", "agent-1");
        var reader = fixture.Agents.Register("agent-1");

        // When
        await fixture.ClearBasicAuth("web");

        // Then
        reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Publish_When_Nothing_Is_Configured()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        await fixture.ClearBasicAuth("web");

        // Then
        fixture.Events.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Publish_An_App_Updated_Event()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();
        await fixture.SetBasicAuth("web", "admin", "s3cret");

        // When
        await fixture.ClearBasicAuth("web", actor: "operator@example.com");

        // Then
        fixture.Events.Published[^1].ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.AppUpdated),
            evt => evt.Actor.ShouldBe("operator@example.com"),
            evt => evt.App.ShouldBe("web"),
            evt => evt.Message.ShouldContain("disabled"));
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.ClearBasicAuth("ghost"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
