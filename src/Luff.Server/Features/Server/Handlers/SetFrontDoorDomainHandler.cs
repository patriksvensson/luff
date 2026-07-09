namespace Luff.Server.Features;

public sealed class SetFrontDoorDomainHandler : IRequestHandler<SetFrontDoorDomainHandler.Request, ServerResponse>
{
    private readonly LuffDbContext _database;
    private readonly FrontDoorConfigurator _frontDoor;

    public sealed class Request : IRequest<ServerResponse>
    {
        public string Domain { get; }

        public Request(string domain)
        {
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
        }
    }

    public SetFrontDoorDomainHandler(LuffDbContext database, FrontDoorConfigurator frontDoor)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _frontDoor = frontDoor ?? throw new ArgumentNullException(nameof(frontDoor));
    }

    public async Task<ServerResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var domain = request.Domain.Trim();
        if (domain.Length == 0)
        {
            throw new InvalidDomainException();
        }

        var settings = await _database.ServerSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new ServerSettings { FrontDoorDomain = domain };
            _database.ServerSettings.Add(settings);
        }
        else
        {
            settings.FrontDoorDomain = domain;
        }

        await _database.SaveChangesAsync(cancellationToken);

        _frontDoor.ConfigureConnected(domain);

        return new ServerResponse(domain);
    }
}

public static class SetFrontDoorDomainHandlerExtensions
{
    public static async Task<ServerResponse> SetFrontDoorDomain(
        this ISender sender, string domain, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new SetFrontDoorDomainHandler.Request(domain), cancellationToken);
    }
}
