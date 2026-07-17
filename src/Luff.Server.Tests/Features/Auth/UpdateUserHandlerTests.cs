using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class UpdateUserHandlerTests
{
    [Fact]
    public async Task Should_Update_Role_And_Name()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice@example.com", "secret", UserRole.Operator);

        // When
        var result = await fixture.UpdateUser(
            new UpdateUserHandler.Request("alice@example.com", "Admin", "Ada", "Lovelace"));

        // Then
        result.ShouldSatisfyAllConditions(
            response => response.Role.ShouldBe("Admin"),
            response => response.Email.ShouldBe("alice@example.com"),
            response => response.FirstName.ShouldBe("Ada"),
            response => response.LastName.ShouldBe("Lovelace"));
    }

    [Fact]
    public async Task Should_Keep_The_Users_Own_Email()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice@example.com", "secret", UserRole.Operator);

        // When
        var result = await fixture.UpdateUser(
            new UpdateUserHandler.Request("alice@example.com", "Operator"));

        // Then
        result.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task Should_Not_Change_Emails_When_Updating_A_User()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice@example.com", "secret", UserRole.Operator);
        await fixture.HasUser("bob@example.com", "secret", UserRole.Operator);

        // When
        var result = await fixture.UpdateUser(new UpdateUserHandler.Request("bob@example.com", "Admin"));

        // Then
        result.Email.ShouldBe("bob@example.com");
        (await fixture.FindUser("alice@example.com")).ShouldNotBeNull().Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task Should_Reset_The_Password_And_Revoke_Sessions()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice@example.com", "old", UserRole.Operator);
        await fixture.CreateRefreshTokenService().IssueAsync("alice@example.com", CancellationToken.None);

        // When
        await fixture.UpdateUser(
            new UpdateUserHandler.Request("alice@example.com", "Operator", newPassword: "new"));

        // Then
        var user = await fixture.FindUser("alice@example.com");
        PasswordHasher.Verify("new", user!.PasswordHash).ShouldBeTrue();
        (await fixture.GetRefreshTokens("alice@example.com")).ShouldAllBe(entry => entry.RevokedAt != null);
    }

    [Fact]
    public async Task Should_Not_Touch_The_Password_When_Left_Blank()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice@example.com", "keep", UserRole.Operator);

        // When
        await fixture.UpdateUser(new UpdateUserHandler.Request("alice@example.com", "Operator"));

        // Then
        var user = await fixture.FindUser("alice@example.com");
        PasswordHasher.Verify("keep", user!.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Reject_Demoting_The_Last_Admin()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.UpdateUser(new UpdateUserHandler.Request("admin@example.com", "Operator")));

        // Then
        exception.ShouldBeOfType<LastAdminException>();
    }

    [Fact]
    public async Task Should_Allow_Demoting_An_Admin_When_Another_Remains()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        await fixture.HasUser("second@example.com", "secret", UserRole.Admin);

        // When
        var result = await fixture.UpdateUser(
            new UpdateUserHandler.Request("admin@example.com", "Operator"));

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
            fixture.UpdateUser(new UpdateUserHandler.Request("ghost@example.com", "Operator")));

        // Then
        exception.ShouldBeOfType<UserNotFoundException>();
    }
}
