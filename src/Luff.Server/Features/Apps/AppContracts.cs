namespace Luff.Server.Features;

public sealed class CreateAppRequest
{
    public string Name { get; }
    public AppKind? Kind { get; }
    public string Image { get; }
    public string? Domain { get; }
    public int InternalPort { get; }
    public TlsMode? TlsMode { get; }

    public CreateAppRequest(
        string name, string image, int internalPort,
        AppKind? kind = null, string? domain = null, TlsMode? tlsMode = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Image = image ?? throw new ArgumentNullException(nameof(image));
        InternalPort = internalPort;
        Kind = kind;
        Domain = domain;
        TlsMode = tlsMode;
    }
}

public sealed class UpdateAppRequest
{
    public string Image { get; }
    public string? Domain { get; }
    public int InternalPort { get; }
    public TlsMode? TlsMode { get; }

    public UpdateAppRequest(string image, int internalPort, string? domain = null, TlsMode? tlsMode = null)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        InternalPort = internalPort;
        Domain = domain;
        TlsMode = tlsMode;
    }
}

public sealed class HealthCheckContract
{
    public AppHealthCheckType Type { get; }
    public string? Endpoint { get; }
    public int TimeoutSeconds { get; }

    public HealthCheckContract(AppHealthCheckType type, string? endpoint, int timeoutSeconds)
    {
        Type = type;
        Endpoint = endpoint;
        TimeoutSeconds = timeoutSeconds;
    }
}

public sealed class AppResponse
{
    public string Name { get; }
    public AppKind Kind { get; }
    public string Image { get; }
    public string? Domain { get; }
    public TlsMode TlsMode { get; }
    public int InternalPort { get; }
    public bool Stopped { get; }
    public string? CurrentImageTag { get; }
    public string? PreviousImageTag { get; }
    public HealthCheckContract HealthCheck { get; }

    public AppResponse(
        string name, AppKind kind, string image, string? domain, TlsMode tlsMode, int internalPort, bool stopped,
        string? currentImageTag, string? previousImageTag, HealthCheckContract healthCheck)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        Image = image ?? throw new ArgumentNullException(nameof(image));
        Domain = domain;
        TlsMode = tlsMode;
        InternalPort = internalPort;
        Stopped = stopped;
        CurrentImageTag = currentImageTag;
        PreviousImageTag = previousImageTag;
        HealthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
    }
}
