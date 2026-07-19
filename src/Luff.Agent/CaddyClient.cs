using System.Net;
using System.Text;
using System.Text.Json;
using Luff.Protobuf;
using Microsoft.Extensions.Options;

namespace Luff.Agent;

public interface ICaddyClient
{
    Task ConfigureRouteAsync(
        string host, string upstream, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken);
    Task RemoveRouteAsync(string host, CancellationToken cancellationToken);
    Task RerouteAsync(
        string oldHost, string newHost, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken);
    Task ConfigureFrontDoorAsync(string domain, string upstream, bool managedTls, CancellationToken cancellationToken);
}

public sealed class CaddyClient : ICaddyClient, IDisposable
{
    private readonly HttpClient _client;

    public CaddyClient(IOptions<AgentOptions> options)
        : this(CreateClient(options))
    {
    }

    public CaddyClient(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    private static HttpClient CreateClient(IOptions<AgentOptions> options)
    {
        var agent = options?.Value ?? throw new ArgumentNullException(nameof(options));
        return new HttpClient
        {
            BaseAddress = new Uri(agent.CaddyAdminAddress),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task ConfigureRouteAsync(
        string host, string upstream, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken)
    {
        var id = $"luff-{host}";
        var proxyId = GetProxyId(host);
        var upstreams = new[]
        {
            new Dictionary<string, object?>
            {
                ["dial"] = upstream
            }
        };

        // Swap the live upstream by the reverse-proxy handler's own ID, so the swap is oblivious to any handlers
        // (such as a basic-auth gate) that sit in front of the proxy in the route's handler chain
        using var swap = new StringContent(JsonSerializer.Serialize(upstreams), Encoding.UTF8, "application/json");
        var swapped = await _client.PatchAsync($"/id/{proxyId}/upstreams", swap, cancellationToken);
        if (swapped.IsSuccessStatusCode)
        {
            return;
        }

        // Managed (real domain) routes serve HTTPS on the shared :443 server (Caddy auto-obtains the cert).
        // Http and External both serve plain HTTP on :80. External additionally forwards the original HTTPS
        // scheme downstream (a load balancer terminated TLS in front).
        var server = route == TlsRoute.Managed ? "srv443" : "srv0";

        using var creation = new StringContent(
            JsonSerializer.Serialize(Route(
                id, host, upstream, forwardHttps: route == TlsRoute.External, proxyId: proxyId, basicAuth: basicAuth)),
            Encoding.UTF8, "application/json");
        var create = await _client.PostAsync(
            $"/config/apps/http/servers/{server}/routes", creation, cancellationToken);

        create.EnsureSuccessStatusCode();
    }

    public async Task RemoveRouteAsync(string host, CancellationToken cancellationToken)
    {
        await _client.DeleteAsync($"/id/luff-{host}", cancellationToken);
    }

    public async Task RerouteAsync(
        string oldHost, string newHost, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken)
    {
        var upstream = await ReadUpstreamAsync(oldHost, cancellationToken);
        if (upstream is null)
        {
            return;
        }

        if (string.Equals(oldHost, newHost, StringComparison.Ordinal))
        {
            // Same host == same ID, which can't exist twice. Remove then recreate on the target server.
            // A TLS mode change moves the route between :80 and :443
            await RemoveRouteAsync(oldHost, cancellationToken);
            await ConfigureRouteAsync(newHost, upstream, route, basicAuth, cancellationToken);
            return;
        }

        // Different host == distinct ID. Create the new route before dropping the old one (zero-downtime)
        await ConfigureRouteAsync(newHost, upstream, route, basicAuth, cancellationToken);
        await RemoveRouteAsync(oldHost, cancellationToken);
    }

    public async Task ConfigureFrontDoorAsync(
        string domain, string upstream, bool managedTls, CancellationToken cancellationToken)
    {
        var issuer = new Dictionary<string, object?>
        {
            ["module"] = managedTls ? "acme" : "internal",
        };

        var automation = new Dictionary<string, object?>
        {
            ["automation"] = new Dictionary<string, object?>
            {
                ["policies"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["subjects"] = new[] { domain },
                        ["issuers"] = new[] { issuer },
                    },
                },
            },
        };

        // PATCH replaces an existing value in place (an idempotent re-assert with no :443 blip). PUT creates it
        // the first time. Caddy's PUT is create-only and 409s if the path already exists, so a plain PUT here
        // would fail on every re-push (a domain change or an agent reconnect) and tear down the link
        await ConfigureAsync("/config/apps/tls", automation, cancellationToken);

        // The front door shares the seeded :443 server (srv443) with managed app routes. Upsert its route by its
        // stable ID so a re-push (domain change / reconnect) never replaces the server and wipes app routes.
        // Updating the route in place also moves the host matcher when the front-door domain changes
        await UpsertRouteByIdAsync(
            "luff-frontdoor", Route("luff-frontdoor", domain, upstream), "srv443", cancellationToken);

        if (IPAddress.TryParse(domain, out _))
        {
            var policies = new[]
            {
                new Dictionary<string, object?> { ["default_sni"] = domain },
            };

            await ConfigureAsync(
                "/config/apps/http/servers/srv443/tls_connection_policies", policies, cancellationToken);
        }
    }

    private async Task UpsertRouteByIdAsync(
        string id, object route, string server, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(route);

        using (var replacement = new StringContent(json, Encoding.UTF8, "application/json"))
        {
            var replaced = await _client.PatchAsync($"/id/{id}", replacement, cancellationToken);
            if (replaced.IsSuccessStatusCode)
            {
                return;
            }
        }

        using var creation = new StringContent(json, Encoding.UTF8, "application/json");
        var created = await _client.PostAsync(
            $"/config/apps/http/servers/{server}/routes", creation, cancellationToken);
        created.EnsureSuccessStatusCode();
    }

    private async Task ConfigureAsync(string path, object config, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(config);

        using (var replacement = new StringContent(json, Encoding.UTF8, "application/json"))
        {
            var replaced = await _client.PatchAsync(path, replacement, cancellationToken);
            if (replaced.IsSuccessStatusCode)
            {
                return;
            }
        }

        using var creation = new StringContent(json, Encoding.UTF8, "application/json");
        var created = await _client.PutAsync(path, creation, cancellationToken);
        created.EnsureSuccessStatusCode();
    }

    private static string GetProxyId(string host)
    {
        return $"luff-{host}-proxy";
    }

    private static Dictionary<string, object?> Route(
        string id, string host, string upstream,
        bool forwardHttps = false, string? proxyId = null, BasicAuth? basicAuth = null)
    {
        var proxy = new Dictionary<string, object?>
        {
            ["handler"] = "reverse_proxy",
            ["upstreams"] = new[]
            {
                new Dictionary<string, object?> { ["dial"] = upstream },
            },
        };

        if (proxyId is not null)
        {
            // Tag the reverse proxy so its upstream can later be swapped by @id, whatever its position in the chain.
            proxy["@id"] = proxyId;
        }

        if (forwardHttps)
        {
            // External TLS: a load balancer terminated HTTPS in front,
            // so overwrite Caddy's default `X-Forwarded-Proto: http` with `https`.
            proxy["headers"] = new Dictionary<string, object?>
            {
                ["request"] = new Dictionary<string, object?>
                {
                    ["set"] = new Dictionary<string, object?>
                    {
                        ["X-Forwarded-Proto"] = new[] { "https" },
                    },
                },
            };
        }

        // A basic-auth gate must precede the proxy so it challenges before anything is forwarded upstream.
        var handle = new List<Dictionary<string, object?>>();
        if (basicAuth is not null)
        {
            handle.Add(BasicAuthHandler(basicAuth));
        }

        handle.Add(proxy);

        return new Dictionary<string, object?>
        {
            ["@id"] = id,
            ["match"] = new[]
            {
                new Dictionary<string, object?> { ["host"] = new[] { host } },
            },
            ["handle"] = handle,
        };
    }

    private static Dictionary<string, object?> BasicAuthHandler(BasicAuth basicAuth)
    {
        return new Dictionary<string, object?>
        {
            ["handler"] = "authentication",
            ["providers"] = new Dictionary<string, object?>
            {
                ["http_basic"] = new Dictionary<string, object?>
                {
                    ["accounts"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["username"] = basicAuth.Username,
                            // A bcrypt hash starts with "$", so Caddy takes it verbatim (it only base64-decodes
                            // passwords that don't).
                            ["password"] = basicAuth.Hash,
                        },
                    },
                },
            },
        };
    }

    private async Task<string?> ReadUpstreamAsync(string host, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync($"/id/{GetProxyId(host)}/upstreams", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        return document.RootElement[0].TryGetProperty("dial", out var dial)
            ? dial.GetString()
            : null;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}