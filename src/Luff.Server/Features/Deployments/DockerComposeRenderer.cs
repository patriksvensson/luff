using YamlDotNet.Serialization;

namespace Luff.Server.Features;

public static class DockerComposeRenderer
{
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithIndentedSequences()
        .WithQuotingNecessaryStrings()
        .WithNewLine("\n")
        .Build();

    public static string Render(
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
        var mounts = volumes.OrderBy(volume => volume.Target, StringComparer.Ordinal).ToArray();

        var service = new Dictionary<string, object?>
        {
            ["image"] = $"{app.Image}:{tag}",
        };

        var environment = BuildEnvironment(environmentKeys);
        if (environment.Count > 0)
        {
            service["environment"] = environment;
        }

        var serviceVolumes = BuildVolumes(mounts);
        if (serviceVolumes.Count > 0)
        {
            service["volumes"] = serviceVolumes;
        }

        var publishedPorts = BuildPorts(ports);
        if (publishedPorts.Count > 0)
        {
            service["ports"] = publishedPorts;
        }

        var healthCheck = BuildHealthCheck(app);
        if (healthCheck is not null)
        {
            service["healthcheck"] = healthCheck;
        }

        service["labels"] = new Dictionary<string, object?>
        {
            ["luff.managed"] = "true",
            ["luff.app"] = app.Name,
        };
        service["restart"] = "unless-stopped";
        service["networks"] = new Dictionary<string, object?>
        {
            ["luff"] = new Dictionary<string, object?>
            {
                ["aliases"] = new List<string> { alias },
            },
        };

        var compose = new Dictionary<string, object?>
        {
            ["name"] = project,
            ["services"] = new Dictionary<string, object?> { ["app"] = service },
            ["networks"] = new Dictionary<string, object?>
            {
                ["luff"] = new Dictionary<string, object?> { ["external"] = true },
            },
        };

        var volumeDeclarations = BuildVolumeDeclarations(app.Name, mounts);
        if (volumeDeclarations.Count > 0)
        {
            compose["volumes"] = volumeDeclarations;
        }

        return _serializer.Serialize(compose).TrimEnd('\n');
    }

    private static List<string> BuildEnvironment(IEnumerable<string> keys)
    {
        return [.. keys.OrderBy(key => key, StringComparer.Ordinal)];
    }

    private static List<string> BuildVolumes(IReadOnlyList<Volume> volumes)
    {
        var mounts = new List<string>(volumes.Count);
        foreach (var volume in volumes)
        {
            var mount = $"{volume.Source}:{volume.Target}";
            if (volume.ReadOnly)
            {
                mount += ":ro";
            }

            mounts.Add(mount);
        }

        return mounts;
    }

    private static List<string> BuildPorts(IEnumerable<PortMapping> ports)
    {
        return ports
            .OrderBy(port => port.HostPort)
            .Select(mapping => $"127.0.0.1:{mapping.HostPort}:{mapping.ContainerPort}")
            .ToList();
    }

    private static Dictionary<string, object?>? BuildHealthCheck(App app)
    {
        if (app.HealthCheckType != AppHealthCheckType.Http || string.IsNullOrEmpty(app.HealthCheckEndpoint))
        {
            return null;
        }

        var url = $"http://localhost:{app.InternalPort}{app.HealthCheckEndpoint}";
        var test = $"wget -q --spider {url} 2>/dev/null || curl -fsS {url}";

        return new Dictionary<string, object?>
        {
            ["test"] = new List<string> { "CMD-SHELL", test },
            ["interval"] = "5s",
            ["timeout"] = "3s",
            ["retries"] = 3,
            ["start_period"] = $"{app.HealthCheckTimeoutSeconds}s",
        };
    }

    private static Dictionary<string, object?> BuildVolumeDeclarations(string app, IReadOnlyList<Volume> volumes)
    {
        var declarations = new Dictionary<string, object?>();
        var named = volumes
            .Where(volume => !volume.IsBindMount)
            .Select(volume => volume.Source)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var name in named)
        {
            declarations[name] = new Dictionary<string, object?> { ["name"] = $"luff-{app}-{name}" };
        }

        return declarations;
    }
}
