namespace Luff.Server.Infrastructure;

public sealed class DeploymentTagRequiredException : LuffException
{
    public override string Title => "No tag to deploy";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public DeploymentTagRequiredException(string name)
        : base($"No tag was provided and app '{name}' has no current tag to redeploy")
    {
    }
}
