using System.Net.Sockets;

namespace Luff.Agent;

// A single TCP connect attempt to a container's port over the shared network. The agent uses this for the
// readiness gate on services that speak a non-HTTP protocol (e.g. a database).
public interface ITcpProbe
{
    Task<bool> TryConnectAsync(string host, int port, CancellationToken cancellationToken);
}

public sealed class TcpProbe : ITcpProbe
{
    public async Task<bool> TryConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return client.Connected;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
