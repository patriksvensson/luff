namespace Luff.Server.Features;

public static class TokenEndpoints
{
    public static RouteGroupBuilder MapTokenEndpoints(this RouteGroupBuilder group)
    {
        var tokens = group
            .MapGroup("/apps/{name}/tokens")
            .WithTags("Tokens");

        tokens.MapPost("/", Create)
            .WithName("Tokens_Create")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tokens.MapGet("/", List)
            .WithName("Tokens_List")
            .ProducesProblem(StatusCodes.Status404NotFound);

        tokens.MapDelete("/{id:guid}", Revoke)
            .WithName("Tokens_Revoke")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<CreateTokenResponse>> Create(
        string name, CreateTokenRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var token = await sender.CreateWebhookToken(name, request.Name, cancellationToken);
        return TypedResults.Created($"/api/v1/apps/{name}/tokens/{token.Id}", token);
    }

    private static async Task<Ok<IReadOnlyList<TokenResponse>>> List(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var tokens = await sender.ListWebhookTokens(name, cancellationToken);
        return TypedResults.Ok(tokens);
    }

    private static async Task<NoContent> Revoke(
        string name, Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.RevokeWebhookToken(name, id, cancellationToken);
        return TypedResults.NoContent();
    }
}
