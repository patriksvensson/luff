namespace Luff.Server.Features;

public sealed class AddVolumeRequest
{
    public string Source { get; }
    public string Target { get; }
    public bool ReadOnly { get; }

    public AddVolumeRequest(string source, string target, bool readOnly)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ReadOnly = readOnly;
    }
}

public sealed class VolumeResponse
{
    public string Source { get; }
    public string Target { get; }
    public bool ReadOnly { get; }

    public VolumeResponse(string source, string target, bool readOnly)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ReadOnly = readOnly;
    }
}
