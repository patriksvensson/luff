using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class CreateUserHandlerTests
{
    [Fact]
    public async Task Should_Create_A_User()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var result = await fixture.CreateUser(
            new CreateUserHandler.Request("alice", "secret", "Operator"));

        // Then
        result.ShouldSatisfyAllConditions(
            response => response.Username.ShouldBe("alice"),
            response => response.Role.ShouldBe("Operator"));
    }

    [Fact]
    public async Task Should_Store_The_Password_Hashed()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        await fixture.CreateUser(
            new CreateUserHandler.Request("alice", "secret", "Admin"));

        // Then
        var user = await fixture.FindUser("alice");
        user.ShouldNotBeNull().PasswordHash.ShouldNotBe("secret");
        PasswordHasher.Verify("secret", user.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Throw_When_The_User_Already_Exists()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice", "secret", UserRole.Operator);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateUser(
                new CreateUserHandler.Request("alice", "other", "Admin")));

        // Then
        exception.ShouldBeOfType<UserAlreadyExistsException>();
    }

    [Fact]
    public async Task Should_Throw_On_An_Invalid_Role()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateUser(
                new CreateUserHandler.Request("alice", "secret", "Superuser")));

        // Then
        exception.ShouldBeOfType<InvalidUserRoleException>();
    }
}
