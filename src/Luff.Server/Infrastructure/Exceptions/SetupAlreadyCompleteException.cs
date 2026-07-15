namespace Luff.Server.Infrastructure;

public sealed class SetupAlreadyCompleteException : LuffException
{
    public override string Title => "Setup already complete";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public SetupAlreadyCompleteException()
        : base("Setup has already been completed; an account already exists")
    {
    }
}
