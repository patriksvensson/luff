using Luff.Server.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Audit;

public sealed class EventPublisherTests
{
    [Fact]
    public async Task Should_Fan_Out_The_Same_Event_To_Every_Listener_And_Assign_An_Id()
    {
        // Given
        var first = new RecordingListener();
        var second = new RecordingListener();
        var publisher = new EventPublisher([first, second], NullLogger<EventPublisher>.Instance);

        // When
        await publisher.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.DeploySucceeded,
            Actor = Actors.System,
            Title = "t",
            Message = "m",
        });

        // Then
        var received = first.Received.ShouldHaveSingleItem();
        received.Id.ShouldNotBe(Guid.Empty);
        second.Received.ShouldHaveSingleItem().ShouldBeSameAs(received);
    }

    [Fact]
    public async Task Should_Isolate_A_Failing_Listener_From_The_Others()
    {
        // Given
        var healthy = new RecordingListener();
        var publisher = new EventPublisher(
            [new ThrowingListener(), healthy], NullLogger<EventPublisher>.Instance);

        // When
        var exception = await Record.ExceptionAsync(() => publisher.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.DeployFailed,
            Actor = Actors.System,
            Title = "t",
            Message = "m",
        }));

        // Then
        exception.ShouldBeNull();
        healthy.Received.ShouldHaveSingleItem();
    }

    private sealed class RecordingListener : IEventListener
    {
        public List<AuditEvent> Received { get; } = [];

        public Task OnEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Received.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingListener : IEventListener
    {
        public Task OnEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
