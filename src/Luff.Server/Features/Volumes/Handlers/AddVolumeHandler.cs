namespace Luff.Server.Features;

public sealed class AddVolumeHandler : IRequestHandler<AddVolumeHandler.Request, VolumeResponse>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<VolumeResponse>
    {
        public string AppName { get; }
        public string Source { get; }
        public string Target { get; }
        public bool ReadOnly { get; }
        public string Actor { get; }

        public Request(string appName, string source, string target, bool readOnly, string actor)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            ReadOnly = readOnly;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }
    }

    public AddVolumeHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<VolumeResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var app = await _database.Apps.FindAsync([request.AppName], cancellationToken)
            ?? throw new AppNotFoundException(request.AppName);

        var error = VolumeValidator.Validate(request.Source, request.Target);
        if (error is not null)
        {
            throw new InvalidVolumeException(error);
        }

        var existing = await _database.Volumes.FindAsync([app.Name, request.Target], cancellationToken);
        if (existing is null)
        {
            existing = new Volume
            {
                AppName = app.Name,
                Source = request.Source,
                Target = request.Target,
                ReadOnly = request.ReadOnly,
            };

            _database.Volumes.Add(existing);
        }
        else
        {
            existing.Source = request.Source;
            existing.ReadOnly = request.ReadOnly;
        }

        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.VolumeAdded,
            Actor = request.Actor,
            Title = $"Volume added: {app.Name}",
            Message = $"{existing.Source} was mounted at {existing.Target} on {app.Name}.",
            App = app.Name,
        }, cancellationToken);

        return existing.ToResponse();
    }
}

public static class AddVolumeHandlerExtensions
{
    public static async Task<VolumeResponse> AddVolume(
        this ISender sender, string appName, string source, string target, bool readOnly, string actor,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new AddVolumeHandler.Request(appName, source, target, readOnly, actor), cancellationToken);
    }
}
