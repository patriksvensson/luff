using Luff.Protobuf;

namespace Luff.Agent.Tests.Fakes;

public sealed class FakeCaddyClient : ICaddyClient
{
    public string? Host { get; private set; }
    public string? Upstream { get; private set; }
    public TlsRoute? RouteKind { get; private set; }
    public string? RemovedHost { get; private set; }
    public string? RerouteOldHost { get; private set; }
    public string? RerouteNewHost { get; private set; }
    public TlsRoute? RerouteKind { get; private set; }
    public BasicAuth? RouteBasicAuth { get; private set; }
    public BasicAuth? RerouteBasicAuth { get; private set; }
    public string? FrontDoorDomain { get; private set; }
    public string? FrontDoorUpstream { get; private set; }
    public bool? FrontDoorManagedTls { get; private set; }

    public Task ConfigureRouteAsync(
        string host, string upstream, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken)
    {
        Host = host;
        Upstream = upstream;
        RouteKind = route;
        RouteBasicAuth = basicAuth;
        return Task.CompletedTask;
    }

    public Task RemoveRouteAsync(string host, CancellationToken cancellationToken)
    {
        RemovedHost = host;
        return Task.CompletedTask;
    }

    public Task RerouteAsync(
        string oldHost, string newHost, TlsRoute route, BasicAuth? basicAuth, CancellationToken cancellationToken)
    {
        RerouteOldHost = oldHost;
        RerouteNewHost = newHost;
        RerouteKind = route;
        RerouteBasicAuth = basicAuth;
        return Task.CompletedTask;
    }

    public Task ConfigureFrontDoorAsync(
        string domain, string upstream, bool managedTls, CancellationToken cancellationToken)
    {
        FrontDoorDomain = domain;
        FrontDoorUpstream = upstream;
        FrontDoorManagedTls = managedTls;
        return Task.CompletedTask;
    }
}
