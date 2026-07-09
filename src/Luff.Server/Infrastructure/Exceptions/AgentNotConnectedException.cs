namespace Luff.Server.Infrastructure;

public sealed class AgentNotConnectedException : LuffException
{
    public override string Title => "Agent not connected";
    public override int StatusCode => StatusCodes.Status409Conflict;

    public AgentNotConnectedException(string agent)
        : base($"Agent '{agent}' is not connected")
    {
    }
}
