namespace Luff.Server.Features;

public static class PortValidator
{
    private static readonly HashSet<int> Reserved = [80, 443, 2019, 8080, 8081];

    public static string? Validate(int hostPort, int containerPort)
    {
        if (hostPort < 1024 || hostPort > 65535)
        {
            return "The host port must be between 1024 and 65535";
        }

        if (Reserved.Contains(hostPort))
        {
            return $"The host port {hostPort} is reserved by the Luff stack";
        }

        if (containerPort < 1 || containerPort > 65535)
        {
            return "The container port must be between 1 and 65535";
        }

        return null;
    }
}
