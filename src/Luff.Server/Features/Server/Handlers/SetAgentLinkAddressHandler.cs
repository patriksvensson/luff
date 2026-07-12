namespace Luff.Server.Features;

public sealed class SetAgentLinkAddressHandler : IRequestHandler<SetAgentLinkAddressHandler.Request, ServerResponse>
{
    private readonly LuffDbContext _database;
    private readonly FrontDoorOptions _options;
    private readonly AgentLinkCertificate _certificate;

    public sealed class Request : IRequest<ServerResponse>
    {
        public string Address { get; }

        public Request(string address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }
    }

    public SetAgentLinkAddressHandler(
        LuffDbContext database, IOptions<FrontDoorOptions> options, AgentLinkCertificate certificate)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
    }

    public async Task<ServerResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var address = request.Address.Trim();
        if (address.Length == 0)
        {
            throw new InvalidAgentLinkAddressException();
        }

        var settings = await _database.ServerSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new ServerSettings { FrontDoorDomain = _options.Domain, AgentLinkAddress = address };
            _database.ServerSettings.Add(settings);
        }
        else
        {
            settings.AgentLinkAddress = address;
        }

        await _database.SaveChangesAsync(cancellationToken);

        return new ServerResponse(settings.FrontDoorDomain, settings.AgentLinkAddress, _certificate.Pin);
    }
}

public static class SetAgentLinkAddressHandlerExtensions
{
    public static async Task<ServerResponse> SetAgentLinkAddress(
        this ISender sender, string address, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new SetAgentLinkAddressHandler.Request(address), cancellationToken);
    }
}
