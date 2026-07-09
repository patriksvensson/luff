using Luff.Server.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Infrastructure;

public sealed class SenderTests
{
    [Fact]
    public async Task Should_Route_Request_To_Its_Handler()
    {
        // Given
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<Ping, string>, PingHandler>();
        services.AddScoped<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // When
        var result = await sender.Send(new Ping("hi"), CancellationToken.None);

        // Then
        result.ShouldBe("pong:hi");
    }

    private sealed class Ping : IRequest<string>
    {
        public string Value { get; }

        public Ping(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    private sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"pong:{request.Value}");
        }
    }
}
