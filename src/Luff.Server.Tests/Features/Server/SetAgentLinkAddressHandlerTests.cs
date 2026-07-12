using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Server;

public sealed class SetAgentLinkAddressHandlerTests
{
    [Fact]
    public async Task Should_Persist_The_Agent_Link_Address()
    {
        // Given
        using var fixture = new ServerFixture();

        // When
        await fixture.SetAgentLink("https://cp.example.com:8443");

        // Then
        (await fixture.GetSettingsFromDatabase()).ShouldNotBeNull()
            .AgentLinkAddress.ShouldBe("https://cp.example.com:8443");
    }

    [Fact]
    public async Task Should_Return_The_Address_On_Get()
    {
        // Given
        using var fixture = new ServerFixture();
        await fixture.SetAgentLink("https://cp.example.com:8443");

        // When
        var result = await fixture.GetServer();

        // Then
        result.AgentLinkAddress.ShouldBe("https://cp.example.com:8443");
    }

    [Fact]
    public async Task Should_Reject_An_Empty_Address()
    {
        // Given
        using var fixture = new ServerFixture();

        // When
        var exception = await Record.ExceptionAsync(
            () => fixture.SetAgentLink("  "));

        // Then
        exception.ShouldBeOfType<InvalidAgentLinkAddressException>();
    }
}
