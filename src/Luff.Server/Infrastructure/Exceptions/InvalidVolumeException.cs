namespace Luff.Server.Infrastructure;

public sealed class InvalidVolumeException : LuffException
{
    public override string Title => "Invalid volume";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidVolumeException(string reason)
        : base(reason)
    {
    }
}
