namespace Luff.Server.Infrastructure;

public sealed class AppAlreadyExistsException : LuffException
{
    public override string Title => "App already exists";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public AppAlreadyExistsException(string name)
        : base($"An app named '{name}' already exists")
    {
    }
}