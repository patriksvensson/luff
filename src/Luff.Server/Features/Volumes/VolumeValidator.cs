namespace Luff.Server.Features;

public static partial class VolumeValidator
{
    private static readonly string[] _deniedRoots =
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

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")]
    private static partial Regex NamePattern();

    public static string? Validate(string source, string target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (!target.StartsWith('/') || ContainsTraversal(target))
        {
            return "The target must be an absolute container path without '..'";
        }

        return source.StartsWith('/')
            ? ValidateBindSource(source)
            : ValidateVolumeName(source);
    }

    private static string? ValidateBindSource(string source)
    {
        if (ContainsTraversal(source))
        {
            return "The bind source must not contain '..'";
        }

        if (string.Equals(source, "/", StringComparison.Ordinal) || IsDenied(source))
        {
            return $"The bind source '{source}' is not an allowed host path";
        }

        return null;
    }

    private static string? ValidateVolumeName(string source)
    {
        return NamePattern().IsMatch(source)
            ? null
            : $"'{source}' is not a valid volume name";
    }

    private static bool IsDenied(string source)
    {
        return _deniedRoots.Any(root =>
            string.Equals(source, root, StringComparison.Ordinal) ||
            source.StartsWith($"{root}/", StringComparison.Ordinal));
    }

    private static bool ContainsTraversal(string path)
    {
        return path.Split('/').Contains("..");
    }
}