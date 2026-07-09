using Luff.Server.Infrastructure;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeScopedSender : IScopedSender
{
    private readonly Dictionary<Type, Func<object, object?>> _responders = [];

    public List<object> Sent { get; } = [];

    public void RespondTo<TRequest, TResponse>(Func<TRequest, TResponse> responder)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(responder);

        _responders[typeof(TRequest)] = request => responder((TRequest)request);
    }

    public IReadOnlyList<TRequest> Received<TRequest>()
    {
        return [.. Sent.OfType<TRequest>()];
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Sent.Add(request);

        return _responders.TryGetValue(request.GetType(), out var responder)
            ? Task.FromResult((TResponse)responder(request)!)
            : Task.FromResult(default(TResponse)!);
    }
}
