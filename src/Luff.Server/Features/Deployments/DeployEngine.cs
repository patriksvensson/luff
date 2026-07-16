namespace Luff.Server.Features;

public sealed class DeployEngine
{
    private readonly LuffDbContext _database;
    private readonly IAgentConnections _connections;
    private readonly DockerComposeRenderer _renderer;
    private readonly ISecretProtector _protector;
    private readonly IAlertPublisher _alerts;

    public DeployEngine(
        LuffDbContext database,
        IAgentConnections connections,
        DockerComposeRenderer renderer,
        ISecretProtector protector,
        IAlertPublisher alerts)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
    }

    public async Task<Deployment> QueueDeploymentAsync(App app, string tag, CancellationToken cancellationToken = default)
    {
        if (app.Stopped)
        {
            throw new AppStoppedException(app.Name);
        }

        if (app.Kind == AppKind.Direct)
        {
            var hasPorts = await _database.PortMappings.AnyAsync(
                mapping => mapping.AppName == app.Name, cancellationToken);

            if (!hasPorts)
            {
                throw new DirectAppRequiresPortException(app.Name);
            }
        }

        if (!ImageTagValidator.IsValid(tag))
        {
            throw new InvalidImageTagException(tag);
        }

        var queued = await _database.Deployments.FirstOrDefaultAsync(
            deployment => deployment.AppName == app.Name && deployment.Status == DeploymentStatus.Pending,
            cancellationToken);

        if (queued is null)
        {
            queued = new Deployment
            {
                Id = Guid.NewGuid(),
                AppName = app.Name,
                Tag = tag,
                Status = DeploymentStatus.Pending,
            };

            _database.Deployments.Add(queued);
        }
        else
        {
            queued.Tag = tag;
        }

        await _database.SaveChangesAsync(cancellationToken);
        await TryStartNextDeploymentAsync(app.Name, cancellationToken);

        return queued;
    }

    public async Task TryStartNextDeploymentAsync(string appName, CancellationToken cancellationToken = default)
    {
        var running = await _database.Deployments.AnyAsync(
            deployment => deployment.AppName == appName && deployment.Status == DeploymentStatus.InProgress,
            cancellationToken);

        if (running)
        {
            return;
        }

        var pending = await _database.Deployments.FirstOrDefaultAsync(
            deployment => deployment.AppName == appName && deployment.Status == DeploymentStatus.Pending,
            cancellationToken);

        if (pending is null)
        {
            return;
        }

        var app = await _database.Apps.FindAsync([appName], cancellationToken);
        if (app is null)
        {
            return;
        }

        var agents = await AttachedAgentsAsync(appName, cancellationToken);
        if (agents.Count == 0)
        {
            await FailAsync(pending, "The app is not attached to any agent", cancellationToken);
            return;
        }

        var disconnected = agents.FirstOrDefault(name => !_connections.Connected.Contains(name));
        if (disconnected is not null)
        {
            await FailAsync(pending, $"Agent '{disconnected}' is not connected", cancellationToken);
            return;
        }

        pending.Status = DeploymentStatus.InProgress;
        pending.Agents = [.. agents];
        pending.AgentCursor = 0;
        await _database.SaveChangesAsync(cancellationToken);

        await DispatchAsync(pending, app, pending.Agents[0], cancellationToken);
    }

    public async Task HandleDeployResultAsync(
        string agentName,
        Guid deploymentId,
        bool healthy,
        string? failureReason,
        CancellationToken cancellationToken = default)
    {
        var deployment = await _database.Deployments.FindAsync([deploymentId], cancellationToken);
        if (deployment is null || deployment.Status != DeploymentStatus.InProgress)
        {
            return;
        }

        if (deployment.AgentCursor >= deployment.Agents.Count
            || deployment.Agents[deployment.AgentCursor] != agentName)
        {
            return;
        }

        if (!healthy)
        {
            deployment.Status = DeploymentStatus.Failed;
            deployment.FailureReason = $"Agent '{agentName}': {failureReason}";
            await _database.SaveChangesAsync(cancellationToken);
            await PublishDeployFailedAsync(deployment, failureReason, agentName, cancellationToken);
            await TryStartNextDeploymentAsync(deployment.AppName, cancellationToken);
            return;
        }

        var attachment = await _database.AppAgents.FindAsync([deployment.AppName, agentName], cancellationToken);
        if (attachment is not null)
        {
            attachment.RunningTag = deployment.Tag;
            attachment.RunningDeploymentId = deployment.Id;
        }

        deployment.AgentCursor++;
        await _database.SaveChangesAsync(cancellationToken);

        if (deployment.AgentCursor < deployment.Agents.Count)
        {
            var next = await _database.Apps.FindAsync([deployment.AppName], cancellationToken);
            if (next is not null)
            {
                await DispatchAsync(deployment, next, deployment.Agents[deployment.AgentCursor], cancellationToken);
            }

            return;
        }

        deployment.Status = DeploymentStatus.Succeeded;

        var app = await _database.Apps.FindAsync([deployment.AppName], cancellationToken);
        if (app is not null)
        {
            if (app.CurrentImageTag is not null && app.CurrentImageTag != deployment.Tag)
            {
                app.PreviousImageTag = app.CurrentImageTag;
            }

            app.CurrentImageTag = deployment.Tag;
        }

        await _database.SaveChangesAsync(cancellationToken);
        await _alerts.PublishAsync(new Alert(
            AlertKind.DeploySucceeded,
            $"Deploy succeeded: {deployment.AppName}",
            $"{deployment.AppName} @ {deployment.Tag} is live across {deployment.Agents.Count} "
                + (deployment.Agents.Count == 1 ? "machine." : "machines."),
            deployment.AppName), cancellationToken);
        await TryStartNextDeploymentAsync(deployment.AppName, cancellationToken);
    }

    public async Task HandleAgentDisconnectedAsync(string agentName, CancellationToken cancellationToken = default)
    {
        var inFlight = await _database.Deployments
            .Where(deployment => deployment.Status == DeploymentStatus.InProgress)
            .ToListAsync(cancellationToken);

        foreach (var deployment in inFlight)
        {
            if (deployment.AgentCursor >= deployment.Agents.Count
                || deployment.Agents[deployment.AgentCursor] != agentName)
            {
                continue;
            }

            deployment.Status = DeploymentStatus.Failed;
            deployment.FailureReason = $"Agent '{agentName}' disconnected during the rollout";
            await _database.SaveChangesAsync(cancellationToken);
            await TryStartNextDeploymentAsync(deployment.AppName, cancellationToken);
        }
    }

    public async Task ReconcileOnStartupAsync(CancellationToken cancellationToken = default)
    {
        // Orphaned deployments from a control plane crash never complete (the reporting connection died 
        // with the old process) and block the deploy lane, since TryStartNextDeploymentAsync won't 
        // start a new one while any in-progress row exists. Fail them here so the lane drains. 
        // A redeploy or reconnecting agent will reconcile state
        var orphaned = await _database.Deployments
            .Where(deployment => deployment.Status == DeploymentStatus.InProgress)
            .ToListAsync(cancellationToken);

        if (orphaned.Count == 0)
        {
            return;
        }

        foreach (var deployment in orphaned)
        {
            deployment.Status = DeploymentStatus.Failed;
            deployment.FailureReason = "The control plane restarted during the rollout";
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task CatchUpAgentAsync(string agentName, CancellationToken cancellationToken = default)
    {
        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AgentName == agentName)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            var app = await _database.Apps.FindAsync([attachment.AppName], cancellationToken);
            if (app?.CurrentImageTag is null || attachment.RunningTag == app.CurrentImageTag)
            {
                continue;
            }

            if (app.Stopped)
            {
                // A stopped app stays stopped across reconnects; don't silently redeploy it.
                continue;
            }

            var busy = await _database.Deployments.AnyAsync(
                deployment => deployment.AppName == app.Name
                    && (deployment.Status == DeploymentStatus.Pending
                        || deployment.Status == DeploymentStatus.InProgress),
                cancellationToken);

            if (busy)
            {
                continue;
            }

            var deployment = new Deployment
            {
                Id = Guid.NewGuid(),
                AppName = app.Name,
                Tag = app.CurrentImageTag,
                Status = DeploymentStatus.InProgress,
                Agents = [agentName],
                AgentCursor = 0,
            };

            _database.Deployments.Add(deployment);
            await _database.SaveChangesAsync(cancellationToken);

            await DispatchAsync(deployment, app, agentName, cancellationToken);
        }
    }

    public async Task ReassertRoutesAsync(string agentName, CancellationToken cancellationToken = default)
    {
        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AgentName == agentName)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            if (attachment.RunningDeploymentId is not { } deploymentId)
            {
                continue;
            }

            var app = await _database.Apps.FindAsync([attachment.AppName], cancellationToken);
            if (app?.CurrentImageTag is null || attachment.RunningTag != app.CurrentImageTag)
            {
                // Never deployed here, or behind. CatchUpAgentAsync redeploys a behind agent, which re-pushes
                // the route. Only up-to-date, idle routes need a re-assert
                continue;
            }

            if (!app.IsCaddyFronted)
            {
                // A frontless app has no Caddy route to re-assert.
                continue;
            }

            var busy = await _database.Deployments.AnyAsync(
                deployment => deployment.AppName == app.Name
                    && (deployment.Status == DeploymentStatus.Pending
                        || deployment.Status == DeploymentStatus.InProgress),
                cancellationToken);

            if (busy)
            {
                continue;
            }

            _connections.TrySend(agentName, new ControlMessage
            {
                AssertRoute = new AssertRoute
                {
                    App = app.Name,
                    Domain = app.Domain!,
                    Upstream = $"{DeploymentNaming.Alias(app.Name, deploymentId)}:{app.InternalPort}",
                    Route = TlsRouting.Resolve(app),
                },
            });
        }
    }

    private async Task<IReadOnlyList<string>> AttachedAgentsAsync(string appName, CancellationToken cancellationToken)
    {
        var attachments = await _database.AppAgents
            .Where(attachment => attachment.AppName == appName)
            .ToListAsync(cancellationToken);

        return
        [
            .. attachments
                .OrderBy(attachment => attachment.AttachedAt)
                .Select(attachment => attachment.AgentName),
        ];
    }

    private async Task DispatchAsync(
        Deployment deployment, App app, string agentName, CancellationToken cancellationToken)
    {
        var deploy = await BuildDeployAsync(app, deployment, cancellationToken);
        if (!_connections.TrySend(agentName, new ControlMessage { Deploy = deploy }))
        {
            await FailAsync(deployment, $"Agent '{agentName}' is not connected", cancellationToken);
        }
    }

    private async Task<Deploy> BuildDeployAsync(App app, Deployment deployment, CancellationToken cancellationToken)
    {
        var environmentVariables = (await _database.EnvVars
            .Where(env => env.AppName == app.Name)
            .ToListAsync(cancellationToken))
                .ToDictionary(env => env.Key, env => _protector.Unprotect(env.Value));

        var volumes = await _database.Volumes
            .Where(volume => volume.AppName == app.Name)
            .ToListAsync(cancellationToken);

        var ports = await _database.PortMappings
            .Where(mapping => mapping.AppName == app.Name)
            .ToListAsync(cancellationToken);

        // A frontless app (internal or direct) carries no domain, so the agent skips the Caddy route entirely,
        // and a stable project name makes `docker compose up` recreate in place instead of a parallel stack.
        var frontless = !app.IsCaddyFronted;

        var deploy = new Deploy
        {
            DeploymentId = deployment.Id.ToString(),
            App = deployment.AppName,
            Tag = deployment.Tag,
            Compose = _renderer.Render(app, deployment.Id, deployment.Tag, environmentVariables.Keys, volumes, ports),
            Domain = frontless ? string.Empty : app.Domain,
            InternalPort = app.InternalPort,
            Upstream = frontless
                ? string.Empty
                : $"{DeploymentNaming.Alias(app.Name, deployment.Id)}:{app.InternalPort}",
            Project = frontless
                ? DeploymentNaming.InternalProject(app.Name)
                : DeploymentNaming.Project(app.Name, deployment.Id),
            HealthKind = app.HealthCheckType switch
            {
                AppHealthCheckType.Http => HealthCheckKind.Http,
                AppHealthCheckType.Tcp => HealthCheckKind.Tcp,
                AppHealthCheckType.None => HealthCheckKind.None,
                _ => HealthCheckKind.Docker,
            },
            HealthTimeoutSeconds = app.HealthCheckTimeoutSeconds,
            TlsRoute = frontless ? TlsRoute.Http : TlsRouting.Resolve(app),
        };

        foreach (var entry in environmentVariables)
        {
            deploy.Env.Add(entry.Key, entry.Value);
        }

        var registryHost = Registry.ParseHost(app.Image);
        if (registryHost is not null)
        {
            var registry = await _database.Registries.FindAsync([registryHost], cancellationToken);
            if (registry is not null)
            {
                deploy.Registry = new RegistryCredentials
                {
                    Host = registry.Host,
                    Username = registry.Username,
                    Password = _protector.Unprotect(registry.Password),
                };
            }
        }

        return deploy;
    }

    private async Task FailAsync(Deployment deployment, string reason, CancellationToken cancellationToken)
    {
        deployment.Status = DeploymentStatus.Failed;
        deployment.FailureReason = reason;
        await _database.SaveChangesAsync(cancellationToken);
        await PublishDeployFailedAsync(deployment, reason, agent: null, cancellationToken);
    }

    private Task PublishDeployFailedAsync(
        Deployment deployment, string? detail, string? agent, CancellationToken cancellationToken)
    {
        var suffix = agent is null ? string.Empty : $" on {agent}";
        return _alerts.PublishAsync(new Alert(
            AlertKind.DeployFailed,
            $"Deploy failed: {deployment.AppName}",
            $"{deployment.AppName} @ {deployment.Tag}{suffix}: {detail}",
            deployment.AppName,
            agent), cancellationToken);
    }
}
