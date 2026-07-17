using Luff.Server.Features;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeEventPublisher : IEventPublisher
{
    public List<AuditEvent> Published { get; } = [];

    public Task PublishAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Published.Add(auditEvent);
        return Task.CompletedTask;
    }
}
