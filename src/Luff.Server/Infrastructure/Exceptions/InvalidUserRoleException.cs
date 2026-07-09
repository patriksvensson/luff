namespace Luff.Server.Infrastructure;

public sealed class InvalidUserRoleException : LuffException
{
    public override string Title => "Invalid role";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidUserRoleException(string role)
        : base($"'{role}' is not a valid role")
    {
    }
}
