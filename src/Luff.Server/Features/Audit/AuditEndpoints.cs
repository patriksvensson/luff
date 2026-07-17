namespace Luff.Server.Features;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/audit", ListAudit)
            .WithName("Audit_List")
            .WithTags("Audit");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<AuditEventResponse>>> ListAudit(
        ISender sender, CancellationToken cancellationToken)
    {
        var events = await sender.ListAudit(cancellationToken);
        return TypedResults.Ok(events);
    }
}
