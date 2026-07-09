namespace Luff.Server.Infrastructure;

public sealed class TwoFactorChallengeInvalidException : LuffException
{
    public override string Title => "Two-factor challenge invalid";
    public override int StatusCode => StatusCodes.Status401Unauthorized;

    public TwoFactorChallengeInvalidException()
        : base("The two-factor challenge has expired or is invalid; sign in again")
    {
    }
}
