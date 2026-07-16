using Luff.Server.Features;
using Luff.Server.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Luff.Server.Tests.Volumes;

public sealed class VolumesFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;

    public VolumesFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = TestOptions.For(_connection);

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }

    public async Task<VolumeResponse> AddVolume(AddVolumeHandler.Request request)
    {
        var handler = new AddVolumeHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<IReadOnlyList<VolumeResponse>> ListVolumes(ListVolumesHandler.Request request)
    {
        var handler = new ListVolumesHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task RemoveVolume(RemoveVolumeHandler.Request request)
    {
        var handler = new RemoveVolumeHandler(CreateContext());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task HasApp(string name)
    {
        await using var context = CreateContext();

        context.Apps.Add(new App
        {
            Name = name,
            Image = "nginx",
            Domain = $"{name}.example.com",
            InternalPort = 80,
        });

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Volume>> GetVolumes(string appName)
    {
        await using var context = CreateContext();

        return await context.Volumes
            .Where(volume => volume.AppName == appName)
            .ToListAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
