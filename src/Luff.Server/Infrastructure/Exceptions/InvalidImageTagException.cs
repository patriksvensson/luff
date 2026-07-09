namespace Luff.Server.Infrastructure;

public sealed class InvalidImageTagException : LuffException
{
    public override string Title => "Invalid image tag";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidImageTagException(string tag)
        : base($"The image tag '{tag}' is not a valid Docker tag")
    {
    }
}
