namespace Luff.Server.Features;

public static partial class ImageTagValidator
{
    [GeneratedRegex("^[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string tag)
    {
        return !string.IsNullOrEmpty(tag) && Pattern().IsMatch(tag);
    }
}
