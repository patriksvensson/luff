namespace Luff.Server.Infrastructure;

public sealed class InvalidEmailException : LuffException
{
    public override string Title => "Invalid email";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidEmailException(string email)
        : base($"'{email}' is not a valid email address")
    {
    }
}
