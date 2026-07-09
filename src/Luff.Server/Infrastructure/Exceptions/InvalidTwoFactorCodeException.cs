namespace Luff.Server.Infrastructure;

public sealed class InvalidTwoFactorCodeException : LuffException
{
    public override string Title => "Invalid two-factor code";
    public override int StatusCode => StatusCodes.Status401Unauthorized;

    public InvalidTwoFactorCodeException()
        : base("The two-factor code is invalid")
    {
    }
}
