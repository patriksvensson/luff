using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Ports;

public sealed class ListPortsHandlerTests
{
    [Fact]
    public async Task Should_List_Ports_Ordered_By_Host_Port()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");
        await fixture.AddPort(new AddPortHandler.Request("tool", 8002, 9090));
        await fixture.AddPort(new AddPortHandler.Request("tool", 8001, 3000));

        // When
        var result = await fixture.ListPorts(new ListPortsHandler.Request("tool"));

        // Then
        result.Select(mapping => mapping.HostPort).ShouldBe([8001, 8002]);
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new PortsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ListPorts(new ListPortsHandler.Request("ghost")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
