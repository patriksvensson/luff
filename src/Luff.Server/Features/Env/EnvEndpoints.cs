namespace Luff.Server.Features;

public static class EnvEndpoints
{
    public static RouteGroupBuilder MapEnvEndpoints(this RouteGroupBuilder group)
    {
        var env = group
            .MapGroup("/apps/{name}/env")
            .WithTags("Env");

        env.MapGet("/", List)
            .WithName("Env_List")
            .ProducesProblem(StatusCodes.Status404NotFound);

        env.MapPut("/{key}", Set)
            .WithName("Env_Set")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        env.MapDelete("/{key}", Unset)
            .WithName("Env_Unset")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<EnvVarResponse>>> List(
        string name, ISender sender, CancellationToken cancellationToken)
    {
        var env = await sender.ListEnvVars(name, cancellationToken);
        return TypedResults.Ok(env);
    }

    private static async Task<NoContent> Set(
        string name, string key, SetEnvRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        await sender.SetEnvVar(name, key, request?.Value ?? string.Empty, cancellationToken: cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Unset(
        string name, string key, ISender sender, CancellationToken cancellationToken)
    {
        await sender.UnsetEnvVar(name, key, cancellationToken: cancellationToken);
        return TypedResults.NoContent();
    }
}
