namespace Luff.Agent.Tests.Fakes;

public sealed class FakeTcpProbe : ITcpProbe
{
    public bool Connects { get; set; } = true;
    public string? Host { get; private set; }
    public int Port { get; private set; }
    public int Attempts { get; private set; }

    public Task<bool> TryConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        Host = host;
        Port = port;
        Attempts++;
        return Task.FromResult(Connects);
    }
}
