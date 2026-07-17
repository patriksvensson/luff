using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class DeleteUserHandlerTests
{
    [Fact]
    public async Task Should_Delete_The_User()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        await fixture.HasUser("alice@example.com", "secret", UserRole.Operator);

        // When
        await fixture.DeleteUser(new DeleteUserHandler.Request("alice@example.com"));

        // Then
        (await fixture.FindUser("alice@example.com")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Revoke_The_Users_Sessions()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        await fixture.HasUser("alice@example.com", "secret", UserRole.Operator);
        await fixture.CreateRefreshTokenService().IssueAsync("alice@example.com", CancellationToken.None);

        // When
        await fixture.DeleteUser(new DeleteUserHandler.Request("alice@example.com"));

        // Then
        (await fixture.GetRefreshTokens("alice@example.com")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Reject_Removing_The_Last_Admin()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.DeleteUser(new DeleteUserHandler.Request("admin@example.com")));

        // Then
        exception.ShouldBeOfType<LastAdminException>();
    }

    [Fact]
    public async Task Should_Allow_Removing_An_Admin_When_Another_Remains()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        await fixture.HasUser("second@example.com", "secret", UserRole.Admin);

        // When
        await fixture.DeleteUser(new DeleteUserHandler.Request("second@example.com"));

        // Then
        (await fixture.FindUser("second@example.com")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Throw_When_The_User_Is_Missing()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.DeleteUser(new DeleteUserHandler.Request("ghost@example.com")));

        // Then
        exception.ShouldBeOfType<UserNotFoundException>();
    }
}
