namespace Luff.Server.Infrastructure;

public sealed class VolumeNotFoundException : LuffException
{
    public override string Title => "Volume not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public VolumeNotFoundException(string target, string app)
        : base($"No volume mounted at '{target}' exists for app '{app}'")
    {
    }
}
