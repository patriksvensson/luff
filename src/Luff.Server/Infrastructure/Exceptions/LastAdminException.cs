namespace Luff.Server.Infrastructure;

public sealed class LastAdminException : LuffException
{
    public override string Title => "Last administrator";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public LastAdminException()
        : base("This is the only administrator; promote another user to admin first")
    {
    }
}
