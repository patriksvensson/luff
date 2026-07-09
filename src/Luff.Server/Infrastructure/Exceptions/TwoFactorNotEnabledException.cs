namespace Luff.Server.Infrastructure;

public sealed class TwoFactorNotEnabledException : LuffException
{
    public override string Title => "Two-factor not enabled";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public TwoFactorNotEnabledException()
        : base("Two-factor authentication is not enabled for this account")
    {
    }
}
