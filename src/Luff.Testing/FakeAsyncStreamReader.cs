using Grpc.Core;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeAsyncStreamReader<T> : IAsyncStreamReader<T>
{
    private readonly Queue<T> _frames;
    private readonly bool _keepOpen;
    private readonly TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public T Current { get; private set; } = default!;

    public Task Parked => _parked.Task;

    public FakeAsyncStreamReader(IEnumerable<T> frames, bool keepOpen = false)
    {
        _frames = new Queue<T>(frames);
        _keepOpen = keepOpen;
    }

    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        if (_frames.Count > 0)
        {
            Current = _frames.Dequeue();
            return true;
        }

        if (_keepOpen)
        {
            _parked.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return false;
    }
}
