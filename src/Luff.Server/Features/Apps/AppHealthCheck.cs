namespace Luff.Server.Features;

public static partial class AppHealthCheck
{
    [GeneratedRegex("^/[A-Za-z0-9/._~-]*$")]
    private static partial Regex EndpointPattern();

    public static bool IsValidEndpoint(string endpoint)
    {
        return !string.IsNullOrEmpty(endpoint) && EndpointPattern().IsMatch(endpoint);
    }
}
