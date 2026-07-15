using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class ChangePasswordHandlerTests
{
    [Fact]
    public async Task Should_Change_The_Password()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "old", UserRole.Admin);

        // When
        await fixture.ChangePassword(
            new ChangePasswordHandler.Request("admin", "old", "new"));

        // Then
        var user = await fixture.FindUser("admin");
        PasswordHasher.Verify("new", user!.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Revoke_All_Sessions()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "old", UserRole.Admin);
        await fixture.CreateRefreshTokenService().IssueAsync("admin", CancellationToken.None);

        // When
        await fixture.ChangePassword(
            new ChangePasswordHandler.Request("admin", "old", "new"));

        // Then
        (await fixture.GetRefreshTokens("admin"))
            .ShouldAllBe(entry => entry.RevokedAt != null);
    }

    [Fact]
    public async Task Should_Reject_A_Wrong_Current_Password()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "old", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ChangePassword(
                new ChangePasswordHandler.Request("admin", "wrong", "new")));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
    }
}
