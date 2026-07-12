namespace Luff.Server.Features;

public static class ServerEndpoints
{
    public static RouteGroupBuilder MapServerEndpoints(this RouteGroupBuilder group)
    {
        var server = group
            .MapGroup("/server")
            .WithTags("Server");

        server.MapGet("/", GetServer).WithName("Server_Get");

        server.MapPut("/domain", SetDomain)
            .WithName("Server_SetDomain")
            .RequireAuthorization(JwtAuth.AdminPolicy)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        server.MapPut("/agent-link", SetAgentLink)
            .WithName("Server_SetAgentLink")
            .RequireAuthorization(JwtAuth.AdminPolicy)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<Ok<ServerResponse>> GetServer(ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.GetServer(cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<ServerResponse>> SetDomain(
        SetDomainRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.SetFrontDoorDomain(request.Domain, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<ServerResponse>> SetAgentLink(
        SetAgentLinkRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.SetAgentLinkAddress(request.Address, cancellationToken);
        return TypedResults.Ok(response);
    }
}
