using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Luff.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Configure(args);
        var app = Build(builder);

        await DatabaseMigrator.MigrateAsync(app.Services);
        await InitialAdmin.SeedAsync(app.Services);
        await DeploymentReconciler.ReconcileAsync(app.Services);

        await app.RunAsync();
    }

    private static WebApplicationBuilder Configure(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseStaticWebAssets();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
            options.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddOpenApi("v1", options => options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0);

        var connectionString = builder.Configuration.GetConnectionString("Luff") ?? "Data Source=luff.db";
        builder.Services.AddDbContext<LuffDbContext>(options => options.UseSqlite(connectionString));

        var keysDirectory = ResolveKeysDirectory(connectionString);
        Directory.CreateDirectory(keysDirectory);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
            .SetApplicationName("Luff");
        builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

        var signingKey = new SymmetricSecurityKey(ResolveSigningKey(keysDirectory));
        builder.Services.AddSingleton(signingKey);
        builder.Services.AddSingleton<IJwtIssuer, JwtIssuer>();
        builder.Services.AddScoped<RefreshTokenService>();
        builder.Services.AddSingleton<TwoFactorChallenge>();
        builder.Services.AddScoped<TwoFactorService>();
        builder.Services.Configure<InitialAdminOptions>(
            builder.Configuration.GetSection(InitialAdminOptions.SectionName));

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/login";
                options.Cookie.Name = "luff_auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
            })
            .AddCookie(JwtAuth.TwoFactorPendingScheme, options =>
            {
                // Holds only the username between the password step and the code step: short-lived, and
                // carries no role claim, so it is useless for anything but completing 2FA.
                options.Cookie.Name = "luff_2fa";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = JwtAuth.Issuer,
                    ValidAudience = JwtAuth.Audience,
                    IssuerSigningKey = signingKey,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtAuth.RoleClaim,
                };
            });

        builder.Services.AddAuthorization(options =>
            options.AddPolicy(JwtAuth.AdminPolicy, policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireRole(JwtAuth.AdminPolicy)));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(JwtAuth.CredentialsPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                    }));
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<LuffExceptionHandler>();

        builder.Services.AddMessaging(typeof(Program).Assembly);
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.Configure<AgentEnrollmentOptions>(builder.Configuration.GetSection(AgentEnrollmentOptions.SectionName));
        builder.Services.Configure<FrontDoorOptions>(builder.Configuration.GetSection(FrontDoorOptions.SectionName));
        builder.Services.AddSingleton<AgentEnrollmentValidator>();
        builder.Services.AddSingleton<AgentRegistry>();
        builder.Services.AddSingleton<IAgentConnections, AgentConnections>();
        builder.Services.AddSingleton<IDeployEvents, DeployEvents>();
        builder.Services.AddSingleton<IFleetEvents, FleetEvents>();
        builder.Services.AddHttpClient("notifications", client => client.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddSingleton<NotificationDispatcher>();
        builder.Services.AddSingleton<INotificationDispatcher>(sp => sp.GetRequiredService<NotificationDispatcher>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcher>());
        builder.Services.AddScoped<IAlertPublisher, AlertPublisher>();
        builder.Services.AddSingleton<ILogStream, LogStream>();
        builder.Services.AddSingleton<FrontDoorConfigurator>();
        builder.Services.AddSingleton<DockerComposeRenderer>();
        builder.Services.AddScoped<DeployEngine>();
        builder.Services.AddGrpc();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<CredentialVerifier>();
        builder.Services.AddSingleton<IScopedSender, ScopedSender>();

        return builder;
    }

    private static WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.UseExceptionHandler();

        app.UseStaticFiles();

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapOpenApi();

        app.MapGrpcService<AgentLinkService>();

        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        app.MapGroup("/api/v1")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .MapAppEndpoints()
            .MapStatusEndpoints()
            .MapDeploymentEndpoints()
            .MapLogEndpoints()
            .MapTokenEndpoints()
            .MapEnvEndpoints()
            .MapRegistryEndpoints()
            .MapNotificationEndpoints()
            .MapVolumeEndpoints()
            .MapPortEndpoints()
            .MapAgentEndpoints()
            .MapServerEndpoints()
            .MapAuthEndpoints()
            .MapUserEndpoints();

        app.MapWebhookEndpoints();

        app.MapGet("/health", async (LuffDbContext database, CancellationToken cancellationToken) =>
            await database.Database.CanConnectAsync(cancellationToken)
                ? Results.Ok()
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
            .ExcludeFromDescription()
            .AllowAnonymous();

        app.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        })
        .ExcludeFromDescription()
        .DisableAntiforgery();

        return app;
    }

    private static string ResolveKeysDirectory(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        return Path.Combine(directory ?? ".", "keys");
    }

    private static byte[] ResolveSigningKey(string keysDirectory)
    {
        var path = Path.Combine(keysDirectory, "jwt.key");
        if (File.Exists(path))
        {
            return Convert.FromBase64String(File.ReadAllText(path).Trim());
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(path, Convert.ToBase64String(key));
        return key;
    }
}