using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Luff.Agent;

public static class DockerComposeValidator
{
    private static readonly string[] AllowedServiceKeys =
    [
        "image",
        "environment",
        "volumes",
        "ports",
        "healthcheck",
        "labels",
        "restart",
        "networks",
    ];

    private static readonly string[] DeniedRoots =
    [
        "/proc",
        "/sys",
        "/dev",
        "/etc",
        "/boot",
        "/run",
        "/var/run",
        "/var/lib/docker",
    ];

    public static string? Validate(string compose)
    {
        ArgumentNullException.ThrowIfNull(compose);

        YamlMappingNode root;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(compose));

            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                return "The compose project must be a single YAML mapping";
            }

            root = mapping;
        }
        catch (YamlException)
        {
            return "The compose project could not be parsed";
        }

        foreach (var entry in root.Children)
        {
            var key = (entry.Key as YamlScalarNode)?.Value;
            var error = key switch
            {
                "name" => null,
                "services" => ValidateServices(entry.Value),
                "volumes" => ValidateVolumeDeclarations(entry.Value),
                "networks" => ValidateNetworkDeclarations(entry.Value),
                _ => $"The compose project contains an unexpected top-level key '{key ?? "?"}'",
            };

            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateServices(YamlNode node)
    {
        if (node is not YamlMappingNode services)
        {
            return "The compose 'services' section must be a mapping";
        }

        foreach (var service in services.Children)
        {
            var error = ValidateService(service.Value);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateService(YamlNode node)
    {
        if (node is not YamlMappingNode service)
        {
            return "Each compose service must be a mapping";
        }

        foreach (var entry in service.Children)
        {
            var key = (entry.Key as YamlScalarNode)?.Value;
            if (key is null || !AllowedServiceKeys.Contains(key))
            {
                return $"The compose service contains an unexpected key '{key ?? "?"}'";
            }

            if (key == "volumes")
            {
                var error = ValidateServiceVolumes(entry.Value);
                if (error is not null)
                {
                    return error;
                }
            }

            if (key == "ports")
            {
                var error = ValidateServicePorts(entry.Value);
                if (error is not null)
                {
                    return error;
                }
            }
        }

        return null;
    }

    private static string? ValidateServiceVolumes(YamlNode node)
    {
        if (node is not YamlSequenceNode mounts)
        {
            return "The compose service 'volumes' must be a list";
        }

        foreach (var mount in mounts.Children)
        {
            if (mount is not YamlScalarNode { Value: { } value })
            {
                return "Each compose volume must be a 'source:target' string";
            }

            var error = ValidateMount(value);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateServicePorts(YamlNode node)
    {
        if (node is not YamlSequenceNode ports)
        {
            return "The compose service 'ports' must be a list";
        }

        foreach (var port in ports.Children)
        {
            if (port is not YamlScalarNode { Value: { } value })
            {
                return "Each compose port must be a '127.0.0.1:host:container' string";
            }

            var error = ValidatePort(value);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidatePort(string port)
    {
        var parts = port.Split(':');
        if (parts.Length != 3
            || !string.Equals(parts[0], "127.0.0.1", StringComparison.Ordinal)
            || !ushort.TryParse(parts[1], out var host) || host == 0
            || !ushort.TryParse(parts[2], out var container) || container == 0)
        {
            return $"The published port '{port}' must bind loopback as '127.0.0.1:host:container'";
        }

        return null;
    }

    private static string? ValidateMount(string mount)
    {
        var source = mount.Split(':')[0];
        if (!source.StartsWith('/'))
        {
            return null;
        }

        if (source.Split('/').Contains(".."))
        {
            return $"The bind source '{source}' must not contain '..'";
        }

        return source == "/" || IsDenied(source)
            ? $"The bind source '{source}' is not an allowed host path"
            : null;
    }

    private static string? ValidateVolumeDeclarations(YamlNode node)
    {
        if (node is not YamlMappingNode volumes)
        {
            return "The compose 'volumes' section must be a mapping";
        }

        foreach (var declaration in volumes.Children)
        {
            if (declaration.Value is not YamlMappingNode definition)
            {
                return "Each compose volume declaration must be a mapping";
            }

            foreach (var key in definition.Children.Keys)
            {
                if ((key as YamlScalarNode)?.Value != "name")
                {
                    return $"The compose volume declaration contains an unexpected key '{(key as YamlScalarNode)?.Value ?? "?"}'";
                }
            }
        }

        return null;
    }

    private static string? ValidateNetworkDeclarations(YamlNode node)
    {
        if (node is not YamlMappingNode networks)
        {
            return "The compose 'networks' section must be a mapping";
        }

        foreach (var declaration in networks.Children)
        {
            if (declaration.Value is not YamlMappingNode definition)
            {
                return "Each compose network declaration must be a mapping";
            }

            foreach (var key in definition.Children.Keys)
            {
                if ((key as YamlScalarNode)?.Value != "external")
                {
                    return $"The compose network declaration contains an unexpected key '{(key as YamlScalarNode)?.Value ?? "?"}'";
                }
            }
        }

        return null;
    }

    private static bool IsDenied(string source)
    {
        return DeniedRoots.Any(root =>
            source == root || source.StartsWith($"{root}/", StringComparison.Ordinal));
    }
}
