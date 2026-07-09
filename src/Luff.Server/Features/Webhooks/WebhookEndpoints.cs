namespace Luff.Server.Features;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/webhook/{token}", Trigger)
            .WithName("Webhooks_Trigger")
            .WithTags("Webhooks")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }

    private static async Task<Accepted<DeploymentResponse>> Trigger(
        string token, TriggerWebhookRequest? request,
        ISender sender, CancellationToken cancellationToken)
    {
        var deployment = await sender.TriggerWebhook(token, request?.Tag, cancellationToken);
        return TypedResults.Accepted($"/api/v1/apps/{deployment.App}/deployments", deployment);
    }
}