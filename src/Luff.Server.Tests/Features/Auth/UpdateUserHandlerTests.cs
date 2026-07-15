using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class UpdateUserHandlerTests
{
    [Fact]
    public async Task Should_Update_Role_Email_And_Name()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice", "secret", UserRole.Operator, "alice@example.com");

        // When
        var result = await fixture.UpdateUser(
            new UpdateUserHandler.Request("alice", "Admin", "alice.new@example.com", "Ada", "Lovelace"));

        // Then
        result.ShouldSatisfyAllConditions(
            response => response.Role.ShouldBe("Admin"),
            response => response.Email.ShouldBe("alice.new@example.com"),
            response => response.FirstName.ShouldBe("Ada"),
            response => response.LastName.ShouldBe("Lovelace"));
    }

    [Fact]
    public async Task Should_Keep_The_Users_Own_Email()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice", "secret", UserRole.Operator, "alice@example.com");

        // When
        var result = await fixture.UpdateUser(
            new UpdateUserHandler.Request("alice", "Operator", "Alice@Example.com"));

        // Then
        result.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task Should_Reject_An_Email_Used_By_Another_User()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice", "secret", UserRole.Operator, "alice@example.com");
        await fixture.HasUser("bob", "secret", UserRole.Operator, "bob@example.com");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.UpdateUser(new UpdateUserHandler.Request("bob", "Operator", "alice@example.com")));

        // Then
        exception.ShouldBeOfType<EmailAlreadyExistsException>();
    }

    [Fact]
    public async Task Should_Reset_The_Password_And_Revoke_Sessions()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice", "old", UserRole.Operator);
        await fixture.CreateRefreshTokenService().IssueAsync("alice", CancellationToken.None);

        // When
        await fixture.UpdateUser(
            new UpdateUserHandler.Request("alice", "Operator", "alice@example.com", newPassword: "new"));

        // Then
        var user = await fixture.FindUser("alice");
        PasswordHasher.Verify("new", user!.PasswordHash).ShouldBeTrue();
        (await fixture.GetRefreshTokens("alice")).ShouldAllBe(entry => entry.RevokedAt != null);
    }

    [Fact]
    public async Task Should_Not_Touch_The_Password_When_Left_Blank()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice", "keep", UserRole.Operator);

        // When
        await fixture.UpdateUser(new UpdateUserHandler.Request("alice", "Operator", "alice@example.com"));

        // Then
        var user = await fixture.FindUser("alice");
        PasswordHasher.Verify("keep", user!.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Reject_Demoting_The_Last_Admin()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "secret", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.UpdateUser(new UpdateUserHandler.Request("admin", "Operator", "admin@example.com")));

        // Then
        exception.ShouldBeOfType<LastAdminException>();
    }

    [Fact]
    public async Task Should_Allow_Demoting_An_Admin_When_Another_Remains()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin", "secret", UserRole.Admin, "admin@example.com");
        await fixture.HasUser("second", "secret", UserRole.Admin, "second@example.com");

        // When
        var result = await fixture.UpdateUser(
            new UpdateUserHandler.Request("admin", "Operator", "admin@example.com"));

        // Then
        result.Role.ShouldBe("Operator");
    }

    [Fact]
    public async Task Should_Throw_When_The_User_Is_Missing()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.UpdateUser(new UpdateUserHandler.Request("ghost", "Operator", "ghost@example.com")));

        // Then
        exception.ShouldBeOfType<UserNotFoundException>();
    }
}
