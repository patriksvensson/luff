namespace Luff.Server.Features;

public sealed class SetBasicAuthRequest
{
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public sealed class BasicAuthResponse
{
    public bool Configured { get; }
    public string? Username { get; }
    public string? Password { get; }

    public BasicAuthResponse(bool configured, string? username, string? password)
    {
        Configured = configured;
        Username = username;
        Password = password;
    }
}
