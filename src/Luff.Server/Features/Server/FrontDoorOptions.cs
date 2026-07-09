namespace Luff.Server.Features;

public sealed class FrontDoorOptions
{
    public const string SectionName = "FrontDoor";

    public string Domain { get; set; } = "127.0.0.1.sslip.io";
    public string Upstream { get; set; } = "host.docker.internal:8080";
}
