using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class LogoutHandlerTests
{
    [Fact]
    public async Task Should_Revoke_The_Token_Family()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "secret", UserRole.Admin);
        var token = await fixture.CreateRefreshTokenService().IssueAsync("admin", CancellationToken.None);

        // When
        await fixture.Logout(new LogoutHandler.Request(token));

        // Then
        (await fixture.GetRefreshTokens("admin")).ShouldAllBe(entry => entry.RevokedAt != null);
    }
}
