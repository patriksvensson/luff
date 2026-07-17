namespace Luff.Server.Features;

public static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentEndpoints(this RouteGroupBuilder group)
    {
        var agents = group
            .MapGroup("/agents")
            .WithTags("Agents")
            .RequireAuthorization(JwtAuth.AdminPolicy);

        agents.MapGet("/", ListAgents)
            .WithName("Agents_List");

        agents.MapPost("/", Enroll)
            .WithName("Agents_Enroll")
            .ProducesProblem(StatusCodes.Status409Conflict);

        agents.MapDelete("/{name}", Remove)
            .WithName("Agents_Remove")
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Attaching/detaching an app to a machine is app placement, part of the app lifecycle, so it is open to
        // any authenticated user (Operator or Admin) rather than the Admin-only agent management above.
        var attachments = group
            .MapGroup("/agents")
            .WithTags("Agents");

        attachments.MapPut("/{name}/apps/{appName}", Attach)
            .WithName("Agents_Attach")
            .ProducesProblem(StatusCodes.Status404NotFound);

        attachments.MapDelete("/{name}/apps/{appName}", Detach)
            .WithName("Agents_Detach")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static IEnumerable<AgentResponse> ListAgents(AgentRegistry registry)
    {
        return registry.List()
            .Select(agent => new AgentResponse(agent.Name, agent.Status, agent.Version));
    }

    private static async Task<Created<EnrollAgentResponse>> Enroll(
        EnrollAgentRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.EnrollAgent(
            request.Name, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.Created($"/api/v1/agents/{response.Name}", response);
    }

    private static async Task<NoContent> Remove(
        string name, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        await sender.RemoveAgent(name, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Attach(
        string name, string appName, ISender sender, CancellationToken cancellationToken)
    {
        await sender.AttachApp(name, appName, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Detach(
        string name, string appName, ISender sender, CancellationToken cancellationToken)
    {
        await sender.DetachApp(name, appName, cancellationToken);
        return TypedResults.NoContent();
    }
}
