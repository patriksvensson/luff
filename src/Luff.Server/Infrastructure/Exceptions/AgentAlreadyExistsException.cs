namespace Luff.Server.Infrastructure;

public sealed class AgentAlreadyExistsException : LuffException
{
    public override string Title => "Machine already exists";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public AgentAlreadyExistsException(string name)
        : base($"A machine named '{name}' is already enrolled")
    {
    }
}
