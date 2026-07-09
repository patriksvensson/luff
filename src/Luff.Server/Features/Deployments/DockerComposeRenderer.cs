namespace Luff.Server.Features;

public sealed class DockerComposeRenderer
{
    public string Render(
        App app,
        Guid deploymentId,
        string tag,
        IEnumerable<string> environmentKeys,
        IEnumerable<Volume> volumes,
        IEnumerable<PortMapping> ports)
    {
        var project = app.IsCaddyFronted
            ? DeploymentNaming.Project(app.Name, deploymentId)
            : DeploymentNaming.InternalProject(app.Name);
        var alias = app.IsCaddyFronted
            ? DeploymentNaming.Alias(app.Name, deploymentId)
            : DeploymentNaming.InternalAlias(app.Name);
        var image = $"{app.Image}:{tag}";
        var mounts = volumes.OrderBy(volume => volume.Target, StringComparer.Ordinal).ToArray();
        var environment = RenderEnvironment(environmentKeys);
        var serviceVolumes = RenderVolumes(mounts);
        var publishedPorts = RenderPorts(ports);
        var healthCheck = RenderHealthCheck(app);
        var volumeDeclarations = RenderVolumeDeclarations(app.Name, mounts);

        return $"""
            name: {project}
            services:
              app:
                image: {Quote(image)}{environment}{serviceVolumes}{publishedPorts}{healthCheck}
                labels:
                  luff.managed: "true"
                  luff.app: {Quote(app.Name)}
                restart: unless-stopped
                networks:
                  luff:
                    aliases:
                      - {Quote(alias)}
            networks:
              luff:
                external: true{volumeDeclarations}
            """;
    }

    private static string RenderHealthCheck(App app)
    {
        if (app.HealthCheckType != AppHealthCheckType.Http || string.IsNullOrEmpty(app.HealthCheckEndpoint))
        {
            return string.Empty;
        }

        var url = $"http://localhost:{app.InternalPort}{app.HealthCheckEndpoint}";
        var test = $"wget -q --spider {url} 2>/dev/null || curl -fsS {url}";

        var builder = new StringBuilder("\n    healthcheck:");
        builder.Append("\n      test: [\"CMD-SHELL\", ").Append(Quote(test)).Append(']');
        builder.Append("\n      interval: 5s");
        builder.Append("\n      timeout: 3s");
        builder.Append("\n      retries: 3");
        builder.Append("\n      start_period: ").Append(app.HealthCheckTimeoutSeconds).Append('s');

        return builder.ToString();
    }

    private static string RenderVolumes(IReadOnlyList<Volume> volumes)
    {
        if (volumes.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\n    volumes:");
        foreach (var volume in volumes)
        {
            var mount = $"{volume.Source}:{volume.Target}";
            if (volume.ReadOnly)
            {
                mount += ":ro";
            }

            builder.Append("\n      - ").Append(Quote(mount));
        }

        return builder.ToString();
    }

    private static string RenderPorts(IEnumerable<PortMapping> ports)
    {
        var mappings = ports.OrderBy(port => port.HostPort).ToArray();
        if (mappings.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\n    ports:");
        foreach (var mapping in mappings)
        {
            builder.Append("\n      - ").Append(Quote($"127.0.0.1:{mapping.HostPort}:{mapping.ContainerPort}"));
        }

        return builder.ToString();
    }

    private static string RenderVolumeDeclarations(string app, IReadOnlyList<Volume> volumes)
    {
        var named = volumes
            .Where(volume => !volume.IsBindMount)
            .Select(volume => volume.Source)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (named.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\nvolumes:");
        foreach (var name in named)
        {
            builder.Append("\n  ").Append(name).Append(":\n    name: ").Append($"luff-{app}-{name}");
        }

        return builder.ToString();
    }

    private static string RenderEnvironment(IEnumerable<string> keys)
    {
        var ordered = keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\n    environment:");
        foreach (var key in ordered)
        {
            builder.Append("\n      - ").Append(key);
        }

        return builder.ToString();
    }

    private static string Quote(string value)
    {
        value = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{value}\"";
    }
}
