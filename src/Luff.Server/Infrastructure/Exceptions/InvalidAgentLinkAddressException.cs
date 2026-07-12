namespace Luff.Server.Infrastructure;

public sealed class InvalidAgentLinkAddressException : LuffException
{
    public override string Title => "Invalid agent-link address";
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public InvalidAgentLinkAddressException()
        : base("The agent-link address must not be empty")
    {
    }
}
