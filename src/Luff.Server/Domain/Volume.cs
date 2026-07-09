namespace Luff.Server.Features;

public sealed class Volume
{
    public required string AppName { get; init; }
    public required string Source { get; set; }
    public required string Target { get; init; }
    public required bool ReadOnly { get; set; }

    public bool IsBindMount => Source.StartsWith('/');

    public VolumeResponse ToResponse()
    {
        return new VolumeResponse(Source, Target, ReadOnly);
    }
}
