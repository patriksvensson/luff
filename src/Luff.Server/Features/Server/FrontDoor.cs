namespace Luff.Server.Features;

public static class FrontDoor
{
    public static async Task<string> ResolveDomainAsync(
        LuffDbContext database, FrontDoorOptions options, CancellationToken cancellationToken)
    {
        var settings = await database.ServerSettings.FirstOrDefaultAsync(cancellationToken);
        return settings?.FrontDoorDomain ?? options.Domain;
    }
}
