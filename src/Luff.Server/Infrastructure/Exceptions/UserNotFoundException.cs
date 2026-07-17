namespace Luff.Server.Infrastructure;

public sealed class UserNotFoundException : LuffException
{
    public override string Title => "User not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public UserNotFoundException(string email)
        : base($"No user with email '{email}' exists")
    {
    }
}
