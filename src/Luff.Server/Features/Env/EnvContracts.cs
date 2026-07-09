namespace Luff.Server.Features;

public sealed class SetEnvRequest
{
    public string? Value { get; init; }
}

public sealed class EnvVarResponse
{
    public string Key { get; }

    public EnvVarResponse(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }
}
