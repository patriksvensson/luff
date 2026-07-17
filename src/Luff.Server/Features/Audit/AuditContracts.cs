namespace Luff.Server.Features;

public sealed class AuditEventResponse
{
    public Guid Id { get; }
    public AuditEventKind Kind { get; }
    public string Actor { get; }
    public string Title { get; }
    public string Message { get; }
    public string? App { get; }
    public string? Agent { get; }
    public DateTimeOffset CreatedAt { get; }

    public AuditEventResponse(
        Guid id, AuditEventKind kind, string actor, string title, string message,
        string? app, string? agent, DateTimeOffset createdAt)
    {
        Id = id;
        Kind = kind;
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        App = app;
        Agent = agent;
        CreatedAt = createdAt;
    }
}
