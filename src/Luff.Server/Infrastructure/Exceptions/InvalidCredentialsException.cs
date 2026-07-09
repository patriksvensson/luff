namespace Luff.Server.Infrastructure;

public sealed class InvalidCredentialsException : LuffException
{
    public override string Title => "Invalid credentials";
    public override int StatusCode => StatusCodes.Status401Unauthorized;

    public InvalidCredentialsException()
        : base("The credentials are invalid")
    {
    }
}
