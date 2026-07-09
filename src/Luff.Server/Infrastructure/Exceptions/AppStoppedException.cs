namespace Luff.Server.Infrastructure;

public sealed class AppStoppedException : LuffException
{
    public override string Title => "App is stopped";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public AppStoppedException(string name)
        : base($"App '{name}' is stopped; start it before deploying")
    {
    }
}
