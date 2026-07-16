namespace Luff.Server.Features;

public static class AppKinds
{
    private static readonly HashSet<string> _reserved =
        new(StringComparer.OrdinalIgnoreCase) { "server", "agent", "caddy" };

    public static bool IsReservedName(string name) => _reserved.Contains(name);
}
