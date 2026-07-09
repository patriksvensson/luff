using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Ports;

public sealed class AddPortHandlerTests
{
    [Fact]
    public async Task Should_Add_A_Port_Mapping()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");

        // When
        await fixture.AddPort(new AddPortHandler.Request("tool", 8001, 3000));

        // Then
        var stored = await fixture.GetPorts("tool");
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            mapping => mapping.HostPort.ShouldBe(8001),
            mapping => mapping.ContainerPort.ShouldBe(3000));
    }

    [Fact]
    public async Task Should_Upsert_By_Host_Port()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");
        await fixture.AddPort(new AddPortHandler.Request("tool", 8001, 3000));

        // When
        await fixture.AddPort(new AddPortHandler.Request("tool", 8001, 9090));

        // Then
        var stored = await fixture.GetPorts("tool");
        stored.ShouldHaveSingleItem().ContainerPort.ShouldBe(9090);
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Is_Not_Direct()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("web", AppKind.Web);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddPort(new AddPortHandler.Request("web", 8001, 80)));

        // Then
        exception.ShouldBeOfType<InvalidPortException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Host_Port_Is_Reserved()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddPort(new AddPortHandler.Request("tool", 8080, 3000)));

        // Then
        exception.ShouldBeOfType<InvalidPortException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Host_Port_Is_Privileged()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddPort(new AddPortHandler.Request("tool", 443, 3000)));

        // Then
        exception.ShouldBeOfType<InvalidPortException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Host_Port_Is_Taken_By_Another_App()
    {
        // Given
        using var fixture = new PortsFixture();
        await fixture.HasApp("tool");
        await fixture.HasApp("other");
        await fixture.AddPort(new AddPortHandler.Request("other", 8001, 3000));

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddPort(new AddPortHandler.Request("tool", 8001, 3000)));

        // Then
        exception.ShouldBeOfType<InvalidPortException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new PortsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddPort(new AddPortHandler.Request("ghost", 8001, 3000)));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
