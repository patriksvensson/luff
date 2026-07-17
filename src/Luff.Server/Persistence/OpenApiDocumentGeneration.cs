namespace Luff.Server.Persistence;

public static class OpenApiDocumentGeneration
{
    // The GetDocument tool boots the app under this entry assembly to extract the OpenAPI document. It never
    // applies migrations, so any startup side effect that touches the database must be skipped while it runs.
    public static bool InProgress =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
