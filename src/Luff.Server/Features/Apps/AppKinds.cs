namespace Luff.Server.Features;

public static class AppKinds
{
    private static readonly HashSet<string> _reserved =
        new(StringComparer.OrdinalIgnoreCase) { "server", "agent", "caddy" };

    public static AppKind Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AppKind.Web;
        }

        if (!Enum.TryParse<AppKind>(value, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new InvalidAppKindException(value);
        }

        return kind;
    }

    public static bool IsReservedName(string name) => _reserved.Contains(name);
}
