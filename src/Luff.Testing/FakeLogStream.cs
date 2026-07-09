using Luff.Server.Features;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeLogStream : ILogStream
{
    public string? TailedAgent { get; private set; }
    public string? TailedApp { get; private set; }
    public int? TailedCount { get; private set; }

    public IAsyncEnumerable<LogEvent> Tail(
        string agent, string app, int tail, CancellationToken cancellationToken = default)
    {
        TailedAgent = agent;
        TailedApp = app;
        TailedCount = tail;
        return Empty();
    }

    public void PublishChunk(Guid streamId, LogEvent logEvent)
    {
    }

    private static async IAsyncEnumerable<LogEvent> Empty()
    {
        await Task.CompletedTask;
        yield break;
    }
}
