namespace Luff.Server.Features;

public sealed class SetEnvRequest
{
    public string? Value { get; init; }
}

public sealed class EnvVarResponse
{
    public string Key { get; }
    public string Value { get; }

    public EnvVarResponse(string key, string value)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
