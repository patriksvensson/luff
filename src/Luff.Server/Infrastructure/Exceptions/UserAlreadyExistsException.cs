namespace Luff.Server.Infrastructure;

public sealed class UserAlreadyExistsException : LuffException
{
    public override string Title => "User already exists";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public UserAlreadyExistsException(string username)
        : base($"A user named '{username}' already exists")
    {
    }
}
