namespace Luff.Agent;

public sealed class DockerComposeResult
{
    public bool Succeeded { get; }
    public string? Output { get; }

    public DockerComposeResult(bool succeeded, string? output)
    {
        Succeeded = succeeded;
        Output = output;
    }
}