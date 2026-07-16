using System.Diagnostics;
using System.Globalization;
using Luff.Protobuf;

namespace Luff.Agent;

public sealed class DockerComposeRunner : IDockerComposeRunner
{
    public async Task LoginAsync(string host, string username, string password, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            ArgumentList =
            {
                "login",
                host,
                "--username",
                username,
                "--password-stdin",
            },
        };

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("Could not start docker login");

        await process.StandardInput.WriteAsync(password);
        process.StandardInput.Close();

        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Docker login failed: {error}");
        }
    }

    public async Task<DockerComposeResult> PullAsync(
        string composeYaml, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, composeYaml, cancellationToken);

            var info = new ProcessStartInfo("docker")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                ArgumentList =
                {
                    "compose",
                    "-f",
                    file,
                    "pull",
                },
            };

            foreach (var entry in environment)
            {
                info.Environment[entry.Key] = entry.Value;
            }

            using var process = Process.Start(info)
                                ?? throw new InvalidOperationException("Could not start docker compose pull");

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await outputTask;

            return process.ExitCode == 0
                ? new DockerComposeResult(true, null)
                : new DockerComposeResult(false, await errorTask);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DockerComposeResult(false, exception.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public async Task<DockerComposeResult> UpAsync(
        string composeYaml, IReadOnlyDictionary<string, string> environment, int? waitTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, composeYaml, cancellationToken);

            var info = new ProcessStartInfo("docker")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                ArgumentList =
                {
                    "compose",
                    "-f",
                    file,
                    "up",
                    "-d",
                },
            };

            if (waitTimeoutSeconds is { } timeout)
            {
                info.ArgumentList.Add("--wait");
                info.ArgumentList.Add("--wait-timeout");
                info.ArgumentList.Add(timeout.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var entry in environment)
            {
                info.Environment[entry.Key] = entry.Value;
            }

            using var process = Process.Start(info)
                                ?? throw new InvalidOperationException("Could not start docker compose");

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await outputTask;

            return process.ExitCode == 0
                ? new DockerComposeResult(true, null)
                : new DockerComposeResult(false, await errorTask);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DockerComposeResult(false, exception.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public async Task PruneAsync(string app, string keepProject, CancellationToken cancellationToken)
    {
        var all = await GetContainersAsync([$"label=luff.app={app}"], cancellationToken);
        var green = await GetContainersAsync(
            [$"label=luff.app={app}", $"label=com.docker.compose.project={keepProject}"],
            cancellationToken);

        var stale = all.Except(green).ToArray();
        if (stale.Length > 0)
        {
            await RemoveContainersAsync(stale, cancellationToken);
        }
    }

    public async Task RemoveAppAsync(string app, CancellationToken cancellationToken)
    {
        var containers = await GetContainersAsync([$"label=luff.app={app}"], cancellationToken);
        if (containers.Count > 0)
        {
            await RemoveContainersAsync(containers, cancellationToken);
        }
    }

    public async Task StopAppAsync(string app, CancellationToken cancellationToken)
    {
        var containers = await GetContainersAsync([$"label=luff.app={app}"], cancellationToken);
        if (containers.Count > 0)
        {
            await RunDockerAsync(["stop", .. containers], cancellationToken);
        }
    }

    public async Task StartAppAsync(string app, CancellationToken cancellationToken)
    {
        var containers = await GetContainersAsync([$"label=luff.app={app}"], cancellationToken);
        if (containers.Count > 0)
        {
            await RunDockerAsync(["start", .. containers], cancellationToken);
        }
    }

    public async Task<ContainerStatus?> InspectAsync(string app, CancellationToken cancellationToken)
    {
        var container = await GetRunningContainerAsync(app, cancellationToken)
            ?? (await GetContainersAsync([$"label=luff.app={app}"], cancellationToken)).FirstOrDefault();

        if (container is null)
        {
            return null;
        }

        var format = "{{.State.Running}}|{{.State.Restarting}}|{{.RestartCount}}|{{.State.ExitCode}}|"
            + "{{if .State.Health}}{{.State.Health.Status}}{{end}}";
        var output = await CaptureDockerAsync(["inspect", "--format", format, container], cancellationToken);

        var parts = output.Trim().Split('|');
        if (parts.Length < 5)
        {
            return null;
        }

        return new ContainerStatus(
            Running: string.Equals(parts[0], "true", StringComparison.OrdinalIgnoreCase),
            Restarting: string.Equals(parts[1], "true", StringComparison.OrdinalIgnoreCase),
            RestartCount: int.TryParse(parts[2], out var restarts) ? restarts : 0,
            ExitCode: int.TryParse(parts[3], out var code) ? code : null,
            Health: ParseInspectHealth(parts[4]));
    }

    public async Task<IReadOnlyList<ContainerReport>> ListManagedAsync(CancellationToken cancellationToken)
    {
        var format = "{{.Label \"luff.app\"}}|{{.State}}|{{.Status}}";
        var output = await CaptureDockerAsync(
            ["ps", "--all", "--filter", "label=luff.managed", "--format", format], cancellationToken);

        var reports = new List<ContainerReport>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|');
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[0]))
            {
                continue;
            }

            var state = parts[1];
            var statusText = parts.Length > 2 ? parts[2] : string.Empty;
            var health = ParsePsHealth(statusText);
            var detail = health != DockerHealth.None ? HealthLabel(health) : state;

            reports.Add(new ContainerReport(parts[0], MapRuntimeHealth(state, health), detail));
        }

        return reports;
    }

    private static DockerHealth ParseInspectHealth(string value) => value.Trim().ToLowerInvariant() switch
    {
        "healthy" => DockerHealth.Healthy,
        "unhealthy" => DockerHealth.Unhealthy,
        "starting" => DockerHealth.Starting,
        _ => DockerHealth.None,
    };

    private static DockerHealth ParsePsHealth(string status) =>
        status.Contains("(unhealthy)", StringComparison.OrdinalIgnoreCase) ? DockerHealth.Unhealthy
        : status.Contains("(health: starting)", StringComparison.OrdinalIgnoreCase) ? DockerHealth.Starting
        : status.Contains("(healthy)", StringComparison.OrdinalIgnoreCase) ? DockerHealth.Healthy
        : DockerHealth.None;

    private static RuntimeHealth MapRuntimeHealth(string state, DockerHealth health)
    {
        if (health == DockerHealth.Unhealthy)
        {
            return RuntimeHealth.Unhealthy;
        }

        if (health == DockerHealth.Starting)
        {
            return RuntimeHealth.Starting;
        }

        return state.Trim().ToLowerInvariant() switch
        {
            "running" => RuntimeHealth.Healthy,
            "restarting" => RuntimeHealth.Unhealthy,
            "exited" => RuntimeHealth.Stopped,
            _ => RuntimeHealth.Unknown,
        };
    }

    private static string HealthLabel(DockerHealth health) => health switch
    {
        DockerHealth.Healthy => "healthy",
        DockerHealth.Unhealthy => "unhealthy",
        DockerHealth.Starting => "starting",
        _ => "none",
    };

    public async Task<string?> TailLogsAsync(string app, int lines, CancellationToken cancellationToken)
    {
        var container = await GetRunningContainerAsync(app, cancellationToken)
            ?? (await GetContainersAsync([$"label=luff.app={app}"], cancellationToken)).FirstOrDefault();

        if (container is null)
        {
            return null;
        }

        var output = await CaptureDockerAsync(
            ["logs", "--tail", lines.ToString(CultureInfo.InvariantCulture), container],
            cancellationToken,
            includeStderr: true);

        return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
    }

    public async Task StreamLogsAsync(
        string app, int tail, Action<DockerLogLine> onLine, CancellationToken cancellationToken)
    {
        var container = await GetRunningContainerAsync(app, cancellationToken);
        if (container is null)
        {
            return;
        }

        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList =
            {
                "logs",
                "--follow",
                "--timestamps",
                "--tail",
                tail.ToString(CultureInfo.InvariantCulture),
                container,
            },
        };

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("Could not start docker logs");

        try
        {
            await Task.WhenAll(
                PumpAsync(process.StandardOutput, DockerLogStreamKind.Stdout, onLine, cancellationToken),
                PumpAsync(process.StandardError, DockerLogStreamKind.Stderr, onLine, cancellationToken));
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        DockerLogStreamKind stream,
        Action<DockerLogLine> onLine,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } raw)
        {
            onLine(ParseLine(raw, stream));
        }
    }

    private static DockerLogLine ParseLine(string raw, DockerLogStreamKind stream)
    {
        var space = raw.IndexOf(' ', StringComparison.Ordinal);
        if (space > 0
            && DateTimeOffset.TryParse(
                raw.AsSpan(0, space),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            return new DockerLogLine(timestamp, stream, raw[(space + 1)..]);
        }

        return new DockerLogLine(null, stream, raw);
    }

    private static async Task<string?> GetRunningContainerAsync(string app, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList =
            {
                "ps",
                "--quiet",
                "--filter",
                $"label=luff.app={app}",
            },
        };

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("Could not start docker ps");

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static async Task<IReadOnlyList<string>> GetContainersAsync(
        string[] filters, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList =
            {
                "ps",
                "--all",
                "--quiet",
            }
        };

        foreach (var filter in filters)
        {
            info.ArgumentList.Add("--filter");
            info.ArgumentList.Add(filter);
        }

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("Could not start docker ps");

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static async Task RunDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info);
        if (process is null)
        {
            return;
        }

        await process.WaitForExitAsync(cancellationToken);
    }

    private static async Task<string> CaptureDockerAsync(
        IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool includeStderr = false)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("Could not start docker");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        return includeStderr ? output + await errorTask : output;
    }

    private static async Task RemoveContainersAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList =
            {
                "rm",
                "--force",
            }
        };

        foreach (var id in ids)
        {
            info.ArgumentList.Add(id);
        }

        using var process = Process.Start(info);
        if (process is null)
        {
            return;
        }

        await process.WaitForExitAsync(cancellationToken);
    }
}