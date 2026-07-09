namespace Luff.Server.Infrastructure;

public sealed class Sender : ISender
{
    private static readonly ConcurrentDictionary<Type, HandlerWrapper> _wrappers = new();

    private readonly IServiceProvider _services;

    public Sender(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (HandlerWrapper<TResponse>)_wrappers.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) => CreateWrapper(requestType, responseType),
            typeof(TResponse));

        return wrapper.Handle(request, _services, cancellationToken);
    }

    private static HandlerWrapper CreateWrapper(Type requestType, Type responseType)
    {
        var wrapperType = typeof(HandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
        return (HandlerWrapper)Activator.CreateInstance(wrapperType)!;
    }

    private abstract class HandlerWrapper
    {
    }

    private abstract class HandlerWrapper<TResponse> : HandlerWrapper
    {
        public abstract Task<TResponse> Handle(object request, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class HandlerWrapperImpl<TRequest, TResponse> : HandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Handle(object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            return handler.Handle((TRequest)request, cancellationToken);
        }
    }
}
