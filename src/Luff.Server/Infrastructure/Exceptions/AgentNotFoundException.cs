namespace Luff.Server.Infrastructure;

public sealed class AgentNotFoundException : LuffException
{
    public override string Title => "Agent not found";
    public override int StatusCode => StatusCodes.Status404NotFound;

    public AgentNotFoundException(string name)
        : base($"No agent named '{name}' is known")
    {
    }
}
