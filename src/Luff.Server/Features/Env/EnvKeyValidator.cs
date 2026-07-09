namespace Luff.Server.Features;

public static partial class EnvKeyValidator
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Pattern();

    public static bool IsValid(string key)
    {
        return !string.IsNullOrEmpty(key) && Pattern().IsMatch(key);
    }
}
