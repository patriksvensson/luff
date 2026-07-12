namespace Luff.Server.Features;

public static class AgentJoin
{
    public static string BuildCommand(string repo, string name, string server, string pin, string token)
    {
        var url = $"https://github.com/{repo}/releases/latest/download/agent-install.sh";
        return $"curl -fsSL {url} | sudo sh -s -- "
            + $"--name {Quote(name)} --server {Quote(server)} --pin {Quote(pin)} --token {Quote(token)}";
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }
}
