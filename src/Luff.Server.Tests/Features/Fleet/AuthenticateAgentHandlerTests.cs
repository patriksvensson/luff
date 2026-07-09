using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Fleet;

public sealed class AuthenticateAgentHandlerTests
{
    [Fact]
    public async Task Should_Accept_Matching_Per_Agent_Token_And_Stamp_Last_Seen()
    {
        // Given
        using var fixture = new AuthenticateAgentFixture();
        await fixture.HasTokenAgent("web1", "luff_realtoken");

        // When
        var accepted = await fixture.Authenticate("web1", "luff_realtoken");

        // Then
        accepted.ShouldBeTrue();
        (await fixture.GetAgent("web1"))!.LastSeenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Reject_Wrong_Token_And_The_Bootstrap_Secret_For_A_Token_Agent()
    {
        // Given
        using var fixture = new AuthenticateAgentFixture();
        await fixture.HasTokenAgent("web1", "luff_realtoken");

        // When, Then
        (await fixture.Authenticate("web1", "luff_wrong")).ShouldBeFalse();
        (await fixture.Authenticate("web1", AuthenticateAgentFixture.BootstrapSecret)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Accept_Bootstrap_Secret_For_Unknown_Agent_And_Auto_Register_Without_A_Token()
    {
        // Given
        using var fixture = new AuthenticateAgentFixture();

        // When
        var accepted = await fixture.Authenticate("local", AuthenticateAgentFixture.BootstrapSecret);

        // Then
        accepted.ShouldBeTrue();
        var agent = await fixture.GetAgent("local");
        agent.ShouldNotBeNull();
        agent.EnrollmentTokenHash.ShouldBeNull();
        agent.LastSeenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Reject_An_Unknown_Agent_With_A_Bad_Secret_And_Not_Register_It()
    {
        // Given
        using var fixture = new AuthenticateAgentFixture();

        // When
        var accepted = await fixture.Authenticate("local", "not-the-secret");

        // Then
        accepted.ShouldBeFalse();
        (await fixture.GetAgent("local")).ShouldBeNull();
    }
}
