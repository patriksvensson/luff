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
        await fixture.HasUser("admin", "secret", UserRole.Admin, "admin@example.com");
        await fixture.HasUser("alice", "secret", UserRole.Operator, "alice@example.com");

        // When
        await fixture.DeleteUser(new DeleteUserHandler.Request("alice"));

        // Then
        (await fixture.FindUser("alice")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Revoke_The_Users_Sessions()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "secret", UserRole.Admin, "admin@example.com");
        await fixture.HasUser("alice", "secret", UserRole.Operator, "alice@example.com");
        await fixture.CreateRefreshTokenService().IssueAsync("alice", CancellationToken.None);

        // When
        await fixture.DeleteUser(new DeleteUserHandler.Request("alice"));

        // Then
        (await fixture.GetRefreshTokens("alice")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Reject_Removing_The_Last_Admin()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "secret", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.DeleteUser(new DeleteUserHandler.Request("admin")));

        // Then
        exception.ShouldBeOfType<LastAdminException>();
    }

    [Fact]
    public async Task Should_Allow_Removing_An_Admin_When_Another_Remains()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "secret", UserRole.Admin, "admin@example.com");
        await fixture.HasUser("second", "secret", UserRole.Admin, "second@example.com");

        // When
        await fixture.DeleteUser(new DeleteUserHandler.Request("second"));

        // Then
        (await fixture.FindUser("second")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Throw_When_The_User_Is_Missing()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.DeleteUser(new DeleteUserHandler.Request("ghost")));

        // Then
        exception.ShouldBeOfType<UserNotFoundException>();
    }
}
