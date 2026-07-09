namespace Luff.Server.Infrastructure;

public sealed class DirectAppRequiresPortException : LuffException
{
    public override string Title => "No published port";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public DirectAppRequiresPortException(string app)
        : base($"The direct app '{app}' has no published port; add one before deploying")
    {
    }
}
