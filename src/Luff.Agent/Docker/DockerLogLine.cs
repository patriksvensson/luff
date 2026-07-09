namespace Luff.Agent;

public enum DockerLogStreamKind
{
    Stdout,
    Stderr,
}

public sealed record DockerLogLine(DateTimeOffset? Timestamp, DockerLogStreamKind Stream, string Text);
