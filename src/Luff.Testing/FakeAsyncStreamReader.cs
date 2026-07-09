using Grpc.Core;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeAsyncStreamReader<T> : IAsyncStreamReader<T>
{
    private readonly Queue<T> _frames;

    public T Current { get; private set; } = default!;

    public FakeAsyncStreamReader(IEnumerable<T> frames)
    {
        _frames = new Queue<T>(frames);
    }

    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        if (_frames.Count == 0)
        {
            return false;
        }

        Current = _frames.Dequeue();
        return true;
    }
}
