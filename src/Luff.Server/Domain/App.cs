namespace Luff.Server.Features;

public sealed class App : Entity
{
    public required string Name { get; init; }
    public AppKind Kind { get; set; } = AppKind.Web;
    public required string Image { get; set; }
    public string? Domain { get; set; }
    public required int InternalPort { get; set; }
    public bool Stopped { get; set; }
    public TlsMode TlsMode { get; set; } = TlsMode.Managed;
    public string? CurrentImageTag { get; set; }
    public string? PreviousImageTag { get; set; }
    public AppHealthCheckType HealthCheckType { get; set; } = AppHealthCheckType.Docker;
    public string? HealthCheckEndpoint { get; set; }
    public int HealthCheckTimeoutSeconds { get; set; } = 300;

    public bool IsCaddyFronted => Kind == AppKind.Web;
}

public static class AppExtensions
{
    public static AppResponse ToResponse(this App app)
    {
        return new AppResponse(
            app.Name, app.Kind, app.Image, app.Domain, app.TlsMode, app.InternalPort,
            app.Stopped, app.CurrentImageTag, app.PreviousImageTag,
            new HealthCheckContract(
                app.HealthCheckType, app.HealthCheckEndpoint, app.HealthCheckTimeoutSeconds));
    }
}
