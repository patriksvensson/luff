namespace Luff.Server.Features;

public enum AppHealthState
{
    Live,
    Deploying,
    Drift,
    Dormant,
    Failed,
    Unhealthy,
    Stopped,
}

public static class AppHealth
{
    public static bool IsAutoDomain(string? domain)
    {
        return domain is not null && domain.EndsWith(".sslip.io", StringComparison.OrdinalIgnoreCase);
    }

    public static (AppHealthState State, string? Detail) Classify(
        App app, IReadOnlyList<AppAgent> attachments, Deployment? latest, bool inFlight)
    {
        // Explicit desired state wins over everything else.
        if (app.Stopped)
        {
            return (AppHealthState.Stopped, "Stopped");
        }

        if (attachments.Count == 0)
        {
            return (AppHealthState.Dormant, "Not attached");
        }

        if (inFlight)
        {
            return (AppHealthState.Deploying, null);
        }

        if (app.CurrentImageTag is null)
        {
            return (AppHealthState.Dormant, "Not deployed");
        }

        var behind = attachments.Count(attachment => attachment.RunningTag != app.CurrentImageTag);
        if (behind > 0)
        {
            return (AppHealthState.Drift, $"{behind} behind");
        }

        if (latest?.Status == DeploymentStatus.Failed)
        {
            return (AppHealthState.Failed, null);
        }

        // Live container health, reported by connected agents, overrides the tag-derived "Live".
        var unhealthy = attachments.Count(attachment => attachment.HealthStatus == AppRuntimeHealth.Unhealthy);
        if (unhealthy > 0)
        {
            return (AppHealthState.Unhealthy, unhealthy == attachments.Count ? "Not running" : $"{unhealthy} unhealthy");
        }

        return (AppHealthState.Live, null);
    }

    public static string Relative(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return "Just now";
        }

        if (span < TimeSpan.FromHours(1))
        {
            return $"{(int)span.TotalMinutes}m ago";
        }

        if (span < TimeSpan.FromDays(1))
        {
            return $"{(int)span.TotalHours}h ago";
        }

        return $"{(int)span.TotalDays}d ago";
    }

    public static string LastDeployText(Deployment? latest, DateTimeOffset now)
    {
        if (latest is null)
        {
            return "Never deployed";
        }

        var verb = latest.Status switch
        {
            DeploymentStatus.Failed => "Failed",
            DeploymentStatus.Succeeded => "Deployed",
            _ => "Deploying",
        };

        return $"{verb} {Relative(now - latest.CreatedAt)}";
    }
}
