namespace Luff.Server.Features;

public enum AppKind
{
    /// <summary>
    /// A public HTTP app fronted by Caddy.
    /// It has a domain, gets a route, deploys blue/green
    /// </summary>
    Web = 0,

    /// <summary>
    /// An internal TCP service (e.g. a database).
    /// Reachable only by sibling apps on the same host's network under its bare name.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Accessed directly via a port (i.e. 127.0.0.1:9876).
    /// Usable for things like tailnets.
    /// </summary>
    Direct = 2,
}
