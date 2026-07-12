namespace Luff.Server.Features;

public sealed class InstallerOptions
{
    public const string SectionName = "Installer";

    public string Repo { get; set; } = "patriksvensson/luff";
}