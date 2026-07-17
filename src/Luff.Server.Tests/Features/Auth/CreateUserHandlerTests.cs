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
            new CreateUserHandler.Request(
                "secret", "Operator", "alice@example.com", "admin@example.com", "Ada", "Lovelace"));

        // Then
        result.ShouldSatisfyAllConditions(
            response => response.Role.ShouldBe("Operator"),
            response => response.Email.ShouldBe("alice@example.com"),
            response => response.FirstName.ShouldBe("Ada"),
            response => response.LastName.ShouldBe("Lovelace"));
    }

    [Fact]
    public async Task Should_Store_The_Password_Hashed()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        await fixture.CreateUser(
            new CreateUserHandler.Request("secret", "Admin", "alice@example.com", "admin@example.com"));

        // Then
        var user = await fixture.FindUser("alice@example.com");
        user.ShouldNotBeNull().PasswordHash.ShouldNotBe("secret");
        PasswordHasher.Verify("secret", user.PasswordHash).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Normalize_The_Email()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        await fixture.CreateUser(
            new CreateUserHandler.Request("secret", "Operator", "  Alice@Example.COM  ", "admin@example.com"));

        // Then
        (await fixture.FindUser("alice@example.com"))!.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task Should_Throw_When_The_User_Already_Exists()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("alice@example.com", "secret", UserRole.Operator);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateUser(
                new CreateUserHandler.Request("other", "Admin", "alice@example.com", "admin@example.com")));

        // Then
        exception.ShouldBeOfType<EmailAlreadyExistsException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_Email_Is_Already_Used()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("shared@example.com", "secret", UserRole.Operator);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateUser(
                new CreateUserHandler.Request("secret", "Operator", "SHARED@example.com", "admin@example.com")));

        // Then
        exception.ShouldBeOfType<EmailAlreadyExistsException>();
    }

    [Fact]
    public async Task Should_Throw_On_An_Invalid_Email()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateUser(
                new CreateUserHandler.Request("secret", "Operator", "not-an-email", "admin@example.com")));

        // Then
        exception.ShouldBeOfType<InvalidEmailException>();
    }

    [Fact]
    public async Task Should_Throw_On_An_Invalid_Role()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateUser(
                new CreateUserHandler.Request("secret", "Superuser", "alice@example.com", "admin@example.com")));

        // Then
        exception.ShouldBeOfType<InvalidUserRoleException>();
    }

    [Fact]
    public async Task Should_Publish_A_User_Created_Event()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        await fixture.CreateUser(
            new CreateUserHandler.Request("secret", "Operator", "alice@example.com", "admin@example.com"));

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.UserCreated),
            evt => evt.Actor.ShouldBe("admin@example.com"),
            evt => evt.Title.ShouldContain("alice@example.com"));
    }
}
