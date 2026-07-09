namespace Luff.Server.Infrastructure;

public sealed class NoPreviousDeploymentException : LuffException
{
    public override string Title => "No previous deployment";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public NoPreviousDeploymentException(string name)
        : base($"App '{name}' has no previous tag to roll back to")
    {
    }
}
