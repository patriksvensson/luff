namespace Luff.Server.Features;

public static class AppEndpoints
{
    public static RouteGroupBuilder MapAppEndpoints(this RouteGroupBuilder group)
    {
        var apps = group
            .MapGroup("/apps")
            .WithTags("Apps");

        apps.MapGet("/", ListApps).WithName("Apps_List");
        apps.MapGet("/{name}", GetApp).WithName("Apps_Get").ProducesProblem(StatusCodes.Status404NotFound);
        apps.MapPost("/", CreateApp).WithName("Apps_Create").ProducesProblem(StatusCodes.Status409Conflict);
        apps.MapPut("/{name}", UpdateApp).WithName("Apps_Update").ProducesProblem(StatusCodes.Status404NotFound);
        apps.MapPut("/{name}/health", SetHealthCheck).WithName("Apps_SetHealthCheck")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status400BadRequest);
        apps.MapDelete("/{name}", DeleteApp).WithName("Apps_Delete").ProducesProblem(StatusCodes.Status404NotFound);
        apps.MapPost("/{name}/stop", StopApp).WithName("Apps_Stop").ProducesProblem(StatusCodes.Status404NotFound);
        apps.MapPost("/{name}/start", StartApp).WithName("Apps_Start").ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<AppResponse>> StopApp(
        string name, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var app = await sender.StopApp(name, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.Ok(app);
    }

    private static async Task<Ok<AppResponse>> StartApp(
        string name, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var app = await sender.StartApp(name, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.Ok(app);
    }

    private static async Task<Ok<IReadOnlyList<AppResponse>>> ListApps(ISender sender,
        CancellationToken cancellationToken)
    {
        var apps = await sender.ListApps(cancellationToken);
        return TypedResults.Ok(apps);
    }

    private static async Task<Ok<AppResponse>> GetApp(string name, ISender sender, CancellationToken cancellationToken)
    {
        var app = await sender.GetApp(name, cancellationToken);
        return TypedResults.Ok(app);
    }

    private static async Task<Created<AppResponse>> CreateApp(CreateAppRequest request, ClaimsPrincipal principal,
        ISender sender, CancellationToken cancellationToken)
    {
        var app = await sender.CreateApp(
            request.Name, request.Image, request.InternalPort, principal.Identity?.Name ?? Actors.System,
            request.Kind, request.Domain, request.TlsMode,
            cancellationToken);

        return TypedResults.Created($"/api/v1/apps/{app.Name}", app);
    }

    private static async Task<Ok<AppResponse>> UpdateApp(string name, UpdateAppRequest request,
        ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var app = await sender.UpdateApp(
            name, request.Image, request.InternalPort, principal.Identity?.Name ?? Actors.System,
            request.Domain, request.TlsMode,
            cancellationToken);

        return TypedResults.Ok(app);
    }

    private static async Task<Ok<AppResponse>> SetHealthCheck(string name, HealthCheckContract request,
        ISender sender, CancellationToken cancellationToken)
    {
        var app = await sender.SetHealthCheck(
            name, request.Type, request.Endpoint, request.TimeoutSeconds,
            cancellationToken);

        return TypedResults.Ok(app);
    }

    private static async Task<NoContent> DeleteApp(
        string name, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        await sender.DeleteApp(name, principal.Identity?.Name ?? Actors.System, cancellationToken);
        return TypedResults.NoContent();
    }
}