namespace Luff.Server.Infrastructure;

public sealed class AgentSelectionRequiredException : LuffException
{
    public override string Title => "Agent selection required";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public AgentSelectionRequiredException(string app, int agentCount)
        : base(agentCount == 0
            ? $"App '{app}' is not attached to any agent"
            : $"App '{app}' runs on {agentCount} agents; specify one with '?agent='")
    {
    }
}
