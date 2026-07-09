namespace Luff.Server.Infrastructure;

public sealed class AttachmentNotFoundException : LuffException
{
    public override string Title => "Attachment not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public AttachmentNotFoundException(string agent, string app)
        : base($"App '{app}' is not attached to agent '{agent}'")
    {
    }
}
