namespace Luff.Server.Features;

public sealed class EnvVar
{
    public required string AppName { get; init; }
    public required string Key { get; init; }
    public required string Value { get; set; }

    public EnvVarResponse ToResponse(string value)
    {
        return new EnvVarResponse(Key, value);
    }
}
