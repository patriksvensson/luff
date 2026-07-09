namespace Luff.Server.Features;

public sealed class AddRegistryRequest
{
    public string Host { get; }
    public string Username { get; }
    public string Password { get; }

    public AddRegistryRequest(string host, string username, string password)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }
}

public sealed class RegistryResponse
{
    public string Host { get; }
    public string Username { get; }

    public RegistryResponse(string host, string username)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Username = username ?? throw new ArgumentNullException(nameof(username));
    }
}
