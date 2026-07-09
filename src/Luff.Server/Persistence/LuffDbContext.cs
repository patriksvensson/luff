namespace Luff.Server.Persistence;

public sealed class LuffDbContext : DbContext
{
    public DbSet<App> Apps => Set<App>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<WebhookToken> WebhookTokens => Set<WebhookToken>();
    public DbSet<EnvVar> EnvVars => Set<EnvVar>();
    public DbSet<Registry> Registries => Set<Registry>();
    public DbSet<Volume> Volumes => Set<Volume>();
    public DbSet<PortMapping> PortMappings => Set<PortMapping>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<AppAgent> AppAgents => Set<AppAgent>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<ServerSettings> ServerSettings => Set<ServerSettings>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();

    public LuffDbContext(DbContextOptions<LuffDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<App>(app =>
        {
            app.HasKey(entity => entity.Name);
            app.Property(entity => entity.Kind).HasConversion<string>();
        });

        modelBuilder.Entity<Deployment>(deployment =>
        {
            deployment.HasKey(entity => entity.Id);
            deployment.Property(entity => entity.Status).HasConversion<string>();
            deployment.HasOne<App>().WithMany().HasForeignKey(entity => entity.AppName);
        });

        modelBuilder.Entity<WebhookToken>(token =>
        {
            token.HasKey(entity => entity.Id);
            token.HasIndex(entity => entity.TokenHash).IsUnique();
            token.HasOne<App>().WithMany().HasForeignKey(entity => entity.AppName);
        });

        modelBuilder.Entity<EnvVar>(env =>
        {
            env.HasKey(entity => new { entity.AppName, entity.Key });
            env.HasOne<App>().WithMany().HasForeignKey(entity => entity.AppName);
        });

        modelBuilder.Entity<Registry>().HasKey(registry => registry.Host);

        modelBuilder.Entity<Volume>(volume =>
        {
            volume.HasKey(entity => new { entity.AppName, entity.Target });
            volume.HasOne<App>().WithMany().HasForeignKey(entity => entity.AppName);
        });

        modelBuilder.Entity<PortMapping>(port =>
        {
            port.HasKey(entity => new { entity.AppName, entity.HostPort });
            port.HasOne<App>().WithMany().HasForeignKey(entity => entity.AppName);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(entity => entity.Username);
            user.Property(entity => entity.Role).HasConversion<string>();
        });

        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.HasKey(entity => entity.Id);
            token.HasIndex(entity => entity.TokenHash).IsUnique();
            token.HasIndex(entity => entity.FamilyId);
            token.HasOne<User>().WithMany().HasForeignKey(entity => entity.Username);
        });

        modelBuilder.Entity<RecoveryCode>(code =>
        {
            code.HasKey(entity => entity.Id);
            code.HasIndex(entity => entity.CodeHash).IsUnique();
            code.HasOne<User>().WithMany().HasForeignKey(entity => entity.Username);
        });

        modelBuilder.Entity<AppAgent>(attachment =>
        {
            attachment.HasKey(entity => new { entity.AppName, entity.AgentName });
            attachment.Property(entity => entity.HealthStatus).HasConversion<string>();
            attachment.HasOne<App>().WithMany().HasForeignKey(entity => entity.AppName);
        });

        modelBuilder.Entity<Agent>().HasKey(agent => agent.Name);

        modelBuilder.Entity<ServerSettings>().HasKey(settings => settings.Id);

        modelBuilder.Entity<NotificationChannel>(channel =>
        {
            channel.HasKey(entity => entity.Id);
            channel.Property(entity => entity.Type).HasConversion<string>();
        });
    }
}