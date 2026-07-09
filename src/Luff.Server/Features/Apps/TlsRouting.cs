namespace Luff.Server.Features;

public static class TlsRouting
{
    public static TlsRoute Resolve(App app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The sslip.io defaults are always plain HTTP. A real domain gets Caddy-managed HTTPS unless the
        // operator declares an external TLS terminator (a load balancer in front)
        if (AppHealth.IsAutoDomain(app.Domain))
        {
            return TlsRoute.Http;
        }

        return app.TlsMode == TlsMode.External ? TlsRoute.External : TlsRoute.Managed;
    }

    public static TlsMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TlsMode.Managed;
        }

        if (!Enum.TryParse<TlsMode>(value, ignoreCase: true, out var mode) || !Enum.IsDefined(mode))
        {
            throw new InvalidTlsModeException(value);
        }

        return mode;
    }
}
