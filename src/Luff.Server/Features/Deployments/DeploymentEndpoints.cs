namespace Luff.Server.Features;

public static class DeploymentEndpoints
{
    public static RouteGroupBuilder MapDeploymentEndpoints(this RouteGroupBuilder group)
    {
        var apps = group
            .MapGroup("/apps")
            .WithTags("Deployments");

        apps.MapPost("/{name}/deploy", TriggerDeployment)
            .WithName("Deployments_Trigger")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        apps.MapPost("/{name}/rollback", Rollback)
            .WithName("Deployments_Rollback")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        apps.MapGet("/{name}/deployments", ListDeployments)
            .WithName("Deployments_List")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Accepted<DeploymentResponse>> TriggerDeployment(
        string name, DeployRequest? request, ClaimsPrincipal principal, ISender sender,
        CancellationToken cancellationToken)
    {
        var actor = principal.Identity?.Name ?? Actors.System;
        var deployment = await sender.TriggerDeployment(name, request?.Tag, actor, cancellationToken);
        return TypedResults.Accepted($"/api/v1/apps/{name}/deployments", deployment);
    }

    private static async Task<Accepted<DeploymentResponse>> Rollback(
        string name, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var actor = principal.Identity?.Name ?? Actors.System;
        var deployment = await sender.Rollback(name, actor, cancellationToken);
        return TypedResults.Accepted($"/api/v1/apps/{name}/deployments", deployment);
    }

    private static async Task<Ok<IReadOnlyList<DeploymentResponse>>> ListDeployments(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var deployments = await sender.ListDeployments(name, cancellationToken);
        return TypedResults.Ok(deployments);
    }
}
