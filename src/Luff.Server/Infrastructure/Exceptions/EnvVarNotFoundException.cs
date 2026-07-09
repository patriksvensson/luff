namespace Luff.Server.Infrastructure;

public sealed class EnvVarNotFoundException : LuffException
{
    public override string Title => "Environment variable not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public EnvVarNotFoundException(string key, string app)
        : base($"No environment variable '{key}' exists for app '{app}'")
    {
    }
}
