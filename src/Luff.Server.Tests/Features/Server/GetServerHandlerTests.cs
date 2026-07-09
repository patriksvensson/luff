using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Server;

public sealed class GetServerHandlerTests
{
    [Fact]
    public async Task Should_Return_The_Configured_Default_When_Unset()
    {
        // Given
        using var fixture = new ServerFixture();

        // When
        var result = await fixture.GetServer();

        // Then
        result.FrontDoorDomain.ShouldBe("127.0.0.1.sslip.io");
    }

    [Fact]
    public async Task Should_Return_The_Persisted_Domain()
    {
        // Given
        using var fixture = new ServerFixture();
        await fixture.SetDomain("luff.example.com");

        // When
        var result = await fixture.GetServer();

        // Then
        result.FrontDoorDomain.ShouldBe("luff.example.com");
    }
}
