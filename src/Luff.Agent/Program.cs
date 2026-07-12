namespace Luff.Agent;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

        var serverAddress =
            builder.Configuration.GetSection(AgentOptions.SectionName)[nameof(AgentOptions.ServerAddress)];
        if (serverAddress?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) != false)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IDockerComposeRunner, DockerComposeRunner>();
        builder.Services.AddSingleton<ICaddyClient, CaddyClient>();
        builder.Services.AddSingleton<ITcpProbe, TcpProbe>();
        builder.Services.AddSingleton<AgentDeployRunner>();
        builder.Services.AddSingleton<AgentLogStreamer>();
        builder.Services.AddHostedService<AgentLinkWorker>();

        var host = builder.Build();
        host.Run();
    }
}