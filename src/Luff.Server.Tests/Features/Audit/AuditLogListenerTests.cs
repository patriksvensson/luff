using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Audit;

public sealed class AuditLogListenerTests
{
    [Fact]
    public async Task Should_Persist_The_Event_With_A_Stamped_Timestamp()
    {
        // Given
        using var fixture = new AuditFixture();
        var listener = fixture.CreateAuditLogListener();

        // When
        await listener.OnEventAsync(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            Kind = AuditEventKind.DeploySucceeded,
            Actor = "alice@example.com",
            Title = "Deploy succeeded: web",
            Message = "web @ v2 is live.",
            App = "web",
        }, CancellationToken.None);

        // Then
        var stored = (await fixture.AllEvents()).ShouldHaveSingleItem();
        stored.ShouldSatisfyAllConditions(
            e => e.Kind.ShouldBe(AuditEventKind.DeploySucceeded),
            e => e.Actor.ShouldBe("alice@example.com"),
            e => e.App.ShouldBe("web"),
            e => e.CreatedAt.ShouldBe(fixture.Time.GetUtcNow()));
    }
}
