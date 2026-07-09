namespace Luff.Server.Infrastructure;

public interface IScopedSender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
