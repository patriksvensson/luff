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
        await fixture.HasUser("admin@example.com", "old", UserRole.Admin);

        // When
        await fixture.ChangePassword(
            new ChangePasswordHandler.Request("admin@example.com", "old", "new"));

        // Then
        var user = await fixture.FindUser("admin@example.com");
        PasswordHasher.Verify("new", user!.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Revoke_All_Sessions()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "old", UserRole.Admin);
        await fixture.CreateRefreshTokenService().IssueAsync("admin@example.com", CancellationToken.None);

        // When
        await fixture.ChangePassword(
            new ChangePasswordHandler.Request("admin@example.com", "old", "new"));

        // Then
        (await fixture.GetRefreshTokens("admin@example.com"))
            .ShouldAllBe(entry => entry.RevokedAt != null);
    }

    [Fact]
    public async Task Should_Reject_A_Wrong_Current_Password()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "old", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ChangePassword(
                new ChangePasswordHandler.Request("admin@example.com", "wrong", "new")));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
    }
}
