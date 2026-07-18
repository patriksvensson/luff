using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;

namespace Luff.Server.Features;

public static class LogEndpoints
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly byte[] _newline = "\n"u8.ToArray();

    public static RouteGroupBuilder MapLogEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/apps/{name}/logs", Tail)
            .WithName("Logs_Tail")
            .WithTags("Logs")
            .Produces<Stream>(StatusCodes.Status200OK, "application/x-ndjson")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task Tail(
        string name, string? agent, ISender sender, HttpContext context, IHostApplicationLifetime lifetime)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted, lifetime.ApplicationStopping);
        var token = cts.Token;

        var events = await sender.TailLogs(name, agent, token);

        context.Response.ContentType = "application/x-ndjson";
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            await foreach (var logEvent in events)
            {
                await JsonSerializer.SerializeAsync(context.Response.Body, logEvent, _json, token);
                await context.Response.Body.WriteAsync(_newline, token);
                await context.Response.Body.FlushAsync(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
