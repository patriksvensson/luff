namespace Luff.Server.Infrastructure;

public sealed class InvalidEnvKeyException : LuffException
{
    public override string Title => "Invalid environment variable key";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidEnvKeyException(string key)
        : base($"'{key}' is not a valid environment variable key")
    {
    }
}
