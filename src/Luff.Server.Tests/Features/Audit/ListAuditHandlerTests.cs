using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Audit;

public sealed class ListAuditHandlerTests
{
    [Fact]
    public async Task Should_List_Events_Newest_First()
    {
        // Given
        using var fixture = new AuditFixture();
        await fixture.HasAuditEvent(
            AuditEventKind.DeploySucceeded, "Deploy succeeded: web", age: TimeSpan.FromDays(2));
        await fixture.HasAuditEvent(
            AuditEventKind.AgentDisconnected, "Agent disconnected: agent-1", age: TimeSpan.FromMinutes(5));
        await fixture.HasAuditEvent(
            AuditEventKind.AppStopped, "App stopped: web", age: TimeSpan.FromMinutes(1));

        // When
        var rows = await fixture.ListAudit();

        // Then
        rows.Select(row => row.Title).ShouldBe(
            ["App stopped: web", "Agent disconnected: agent-1", "Deploy succeeded: web"]);
    }

    [Fact]
    public async Task Should_Carry_The_Actor_And_App()
    {
        // Given
        using var fixture = new AuditFixture();
        await fixture.HasAuditEvent(
            AuditEventKind.AppStopped, "App stopped: web", actor: "alice@example.com", app: "web");

        // When
        var row = (await fixture.ListAudit()).ShouldHaveSingleItem();

        // Then
        row.ShouldSatisfyAllConditions(
            r => r.Kind.ShouldBe(AuditEventKind.AppStopped),
            r => r.Actor.ShouldBe("alice@example.com"),
            r => r.App.ShouldBe("web"));
    }

    [Fact]
    public async Task Should_Cap_At_The_Fifty_Most_Recent()
    {
        // Given
        using var fixture = new AuditFixture();
        for (var i = 0; i < 60; i++)
        {
            await fixture.HasAuditEvent(
                AuditEventKind.DeploySucceeded, $"Deploy {i}", age: TimeSpan.FromMinutes(60 - i));
        }

        // When
        var rows = await fixture.ListAudit();

        // Then
        rows.Count.ShouldBe(50);
        rows[0].Title.ShouldBe("Deploy 59");
    }
}
