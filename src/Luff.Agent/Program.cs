namespace Luff.Agent;

public static class Program
{
    public static void Main(string[] args)
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
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
