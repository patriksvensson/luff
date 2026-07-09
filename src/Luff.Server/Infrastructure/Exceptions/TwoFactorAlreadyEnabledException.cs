namespace Luff.Server.Infrastructure;

public sealed class TwoFactorAlreadyEnabledException : LuffException
{
    public override string Title => "Two-factor already enabled";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public TwoFactorAlreadyEnabledException()
        : base("Two-factor authentication is already enabled for this account")
    {
    }
}
