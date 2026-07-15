namespace Luff.Server.Infrastructure;

public sealed class EmailAlreadyExistsException : LuffException
{
    public override string Title => "Email already in use";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public EmailAlreadyExistsException(string email)
        : base($"The email '{email}' is already in use")
    {
    }
}
