namespace Luff.Server.Infrastructure;

public abstract class LuffException : Exception
{
    public abstract string Title { get; }
    public abstract int StatusCode { get; }

    protected LuffException(string message)
        : base(message)
    {
    }
}
