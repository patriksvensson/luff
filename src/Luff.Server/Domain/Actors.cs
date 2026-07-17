namespace Luff.Server.Features;

// The set of non-user actors an audit event can be attributed to. A user action carries the actor's email
// instead; these cover the events no human triggered.
public static class Actors
{
    public const string System = "system";
    public const string Ci = "ci";

    public static string Agent(string name) => $"agent:{name}";
}
