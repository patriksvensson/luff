namespace Luff.Server.Infrastructure;

public sealed class LuffExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public LuffExceptionHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails ?? throw new ArgumentNullException(nameof(problemDetails));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not LuffException domain)
        {
            return false;
        }

        httpContext.Response.StatusCode = domain.StatusCode;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domain,
            ProblemDetails =
            {
                Status = domain.StatusCode,
                Title = domain.Title,
                Detail = domain.Message,
            },
        });
    }
}