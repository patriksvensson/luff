namespace Luff.Server.Features;

public sealed record LogEvent(DateTimeOffset? Timestamp, string Stream, string Line, string? Agent);
