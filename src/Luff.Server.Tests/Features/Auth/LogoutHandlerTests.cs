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
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var token = await fixture.CreateRefreshTokenService().IssueAsync("admin@example.com", CancellationToken.None);

        // When
        await fixture.Logout(new LogoutHandler.Request(token));

        // Then
        (await fixture.GetRefreshTokens("admin@example.com")).ShouldAllBe(entry => entry.RevokedAt != null);
    }
}
