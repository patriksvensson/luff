namespace Luff.Server.Features;

public sealed record LogEvent(DateTimeOffset? Timestamp, LogStreamKind Stream, string Line, string? Agent);
