using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Ports;

public sealed class RemovePortHandlerTests
{
    [Fact]
    public async Task Should_Remove_A_Port_Mapping()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");
        await fixture.AddPort(new AddPortHandler.Request("tool", 8001, 3000));

        // When
        await fixture.RemovePort(new RemovePortHandler.Request("tool", 8001));

        // Then
        (await fixture.GetPorts("tool")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_When_The_Mapping_Does_Not_Exist()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.RemovePort(new RemovePortHandler.Request("tool", 8001)));

        // Then
        exception.ShouldBeOfType<PortMappingNotFoundException>();
    }
}
