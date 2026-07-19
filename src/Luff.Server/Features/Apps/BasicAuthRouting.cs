namespace Luff.Server.Features;

/// <summary>
/// Utilitiy that re-applies an app's Caddy route on every attached agent so a basic-auth change
/// (enable, update, or clear) takes effect live, rather than waiting for the next deploy.
/// It reuses the existing Reroute path with old == new domain: the agent rebuilds the route in place,
/// carrying whatever gate BasicAuthWire.Resolve produces now (a credential, or nothing).
/// Centralising it here keeps the two write paths (set and clear) from ever diverging.
/// </summary>
public static class BasicAuthRouting
{
    public static async Task ReassertAsync(
        LuffDbContext database, IAgentConnections connections, IBasicAuthHasher hasher, ISecretProtector protector,
        App app, CancellationToken cancellationToken)
    {
        if (!app.IsCaddyFronted)
        {
            // A frontless app has no Caddy route to gate.
            return;
        }

        var agents = await database.AppAgents
            .Where(attachment => attachment.AppName == app.Name)
            .Select(attachment => attachment.AgentName)
            .ToListAsync(cancellationToken);

        var (basicAuthUsername, basicAuthHash) = BasicAuthWire.Resolve(app, hasher, protector);
        foreach (var agent in agents)
        {
            connections.TrySend(agent, new ControlMessage
            {
                Reroute = new Reroute
                {
                    App = app.Name,
                    OldDomain = app.Domain!,
                    NewDomain = app.Domain!,
                    Route = TlsRouting.Resolve(app),
                    BasicAuthUsername = basicAuthUsername,
                    BasicAuthHash = basicAuthHash,
                },
            });
        }
    }
}