using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Ports;

public sealed class PortValidatorTests
{
    [Fact]
    public void Should_Accept_A_Valid_Mapping()
    {
        // When
        var result = PortValidator.Validate(8001, 3000);

        // Then
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(80)]
    [InlineData(1023)]
    [InlineData(70000)]
    public void Should_Reject_A_Host_Port_Outside_The_Allowed_Range(int hostPort)
    {
        // When
        var result = PortValidator.Validate(hostPort, 3000);

        // Then
        result.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(8080)]
    [InlineData(8081)]
    public void Should_Reject_A_Reserved_Host_Port(int hostPort)
    {
        // When
        var result = PortValidator.Validate(hostPort, 3000);

        // Then
        result.ShouldBe(
            $"The host port {hostPort} is reserved by the Luff stack");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void Should_Reject_An_Out_Of_Range_Container_Port(int containerPort)
    {
        // When
        var result = PortValidator.Validate(8001, containerPort);

        // Then
        result.ShouldNotBeNull();
    }
}
