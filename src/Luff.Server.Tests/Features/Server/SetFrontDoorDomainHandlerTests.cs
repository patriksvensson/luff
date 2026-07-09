using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Server;

public sealed class SetFrontDoorDomainHandlerTests
{
    [Fact]
    public async Task Should_Persist_The_New_Domain()
    {
        // Given
        using var fixture = new ServerFixture();

        // When
        await fixture.SetDomain("luff.example.com");

        // Then
        (await fixture.GetSettingsFromDatabase()).ShouldNotBeNull()
            .FrontDoorDomain.ShouldBe("luff.example.com");
    }

    [Fact]
    public async Task Should_Re_Push_The_Front_Door_To_Its_Hosts()
    {
        // Given
        using var fixture = new ServerFixture();
        var reader = fixture.HasFrontDoorAgent("agent-1");

        // When
        await fixture.SetDomain("luff.example.com");

        // Then
        reader.TryRead(out var message).ShouldBeTrue();
        message!.ConfigureFrontDoor.Domain.ShouldBe("luff.example.com");
    }

    [Fact]
    public async Task Should_Reject_An_Empty_Domain()
    {
        // Given
        using var fixture = new ServerFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.SetDomain("  "));

        // Then
        exception.ShouldBeOfType<InvalidDomainException>();
    }
}
