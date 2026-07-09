namespace Luff.Server.Features;

public sealed class AgentDeployResultHandler : IRequestHandler<AgentDeployResultHandler.Request, Unit>
{
    private readonly DeployEngine _engine;

    public sealed class Request : IRequest<Unit>
    {
        public string AgentName { get; }
        public Guid DeploymentId { get; }
        public bool Healthy { get; }
        public string? FailureReason { get; }

        public Request(string agentName, Guid deploymentId, bool healthy, string? failureReason)
        {
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            DeploymentId = deploymentId;
            Healthy = healthy;
            FailureReason = failureReason;
        }
    }

    public AgentDeployResultHandler(DeployEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        await _engine.HandleDeployResultAsync(
            request.AgentName,
            request.DeploymentId,
            request.Healthy,
            request.FailureReason,
            cancellationToken);

        return Unit.Value;
    }
}

public static class AgentDeployResultHandlerExtensions
{
    public static async Task AgentDeployResult(
        this IScopedSender sender, string agentName, Guid deploymentId, bool healthy, string? failureReason,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(
            new AgentDeployResultHandler.Request(agentName, deploymentId, healthy, failureReason),
            cancellationToken);
    }
}
