namespace Luff.Server.Infrastructure;

public sealed class AppNotFoundException : LuffException
{
    public override string Title => "App not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public AppNotFoundException(string name)
        : base($"No app named '{name}' exists")
    {
    }
}