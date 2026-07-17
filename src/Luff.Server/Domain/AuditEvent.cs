namespace Luff.Server.Features;

// The system-of-record for anything worth remembering: raised centrally, persisted as the audit log the
// Activity view reads, and fanned out to notification channels. App and Agent are plain labels, not foreign
// keys, so an event outlives the app or agent it describes.
public sealed class AuditEvent : Entity
{
    public Guid Id { get; set; }
    public required AuditEventKind Kind { get; init; }
    public required string Actor { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? App { get; init; }
    public string? Agent { get; init; }

    public AuditEventResponse ToResponse()
    {
        return new AuditEventResponse(Id, Kind, Actor, Title, Message, App, Agent, CreatedAt);
    }
}
