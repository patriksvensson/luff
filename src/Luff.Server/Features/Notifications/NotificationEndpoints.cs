namespace Luff.Server.Features;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        var notifications = group
            .MapGroup("/notifications")
            .WithTags("Notifications")
            .RequireAuthorization(JwtAuth.AdminPolicy);

        notifications.MapPost("/", Add)
            .WithName("Notifications_Add")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        notifications.MapGet("/", List)
            .WithName("Notifications_List");

        notifications.MapDelete("/{id:guid}", Remove)
            .WithName("Notifications_Remove")
            .ProducesProblem(StatusCodes.Status404NotFound);

        notifications.MapPost("/{id:guid}/test", Test)
            .WithName("Notifications_Test")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<NotificationChannelResponse>> Add(
        CreateNotificationChannelRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var channel = await sender.AddNotificationChannel(
            request.Name, request.Type, request.Url, cancellationToken);
        return TypedResults.Created($"/api/v1/notifications/{channel.Id}", channel);
    }

    private static async Task<Ok<IReadOnlyList<NotificationChannelResponse>>> List(
        ISender sender, CancellationToken cancellationToken)
    {
        var channels = await sender.ListNotificationChannels(cancellationToken);
        return TypedResults.Ok(channels);
    }

    private static async Task<NoContent> Remove(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.RemoveNotificationChannel(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Test(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.TestNotificationChannel(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
