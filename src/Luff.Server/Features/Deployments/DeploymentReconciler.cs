namespace Luff.Server.Features;

public static class DeploymentReconciler
{
    public static async Task ReconcileAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<DeployEngine>();
        await engine.ReconcileOnStartupAsync(cancellationToken);
    }
}
