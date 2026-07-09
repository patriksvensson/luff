using Grpc.Core;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeServerStreamWriter<T> : IServerStreamWriter<T>
{
    public List<T> Written { get; } = [];

    public WriteOptions? WriteOptions { get; set; }

    public Task WriteAsync(T message)
    {
        Written.Add(message);
        return Task.CompletedTask;
    }
}
