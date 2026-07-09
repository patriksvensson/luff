namespace Luff.Server.Features;

public static class DeploymentNaming
{
    // Web apps deploy blue/green: each release is its own compose project with a deployment-scoped network
    // alias, so blue and green coexist until Caddy swaps the route.
    public static string Project(string app, Guid deploymentId)
    {
        return $"luff-{app}-{deploymentId:N}";
    }

    public static string Alias(string app, Guid deploymentId)
    {
        return $"{app}-{deploymentId:N}";
    }

    // Internal services deploy by in-place recreate: one stable compose project and a stable, bare-name
    // network alias that sibling apps connect to (e.g. `postgres:5432`).
    public static string InternalProject(string app)
    {
        return $"luff-{app}";
    }

    public static string InternalAlias(string app)
    {
        return app;
    }
}
