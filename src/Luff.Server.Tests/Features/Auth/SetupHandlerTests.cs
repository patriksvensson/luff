using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class SetupHandlerTests
{
    [Fact]
    public async Task Should_Create_The_First_Admin()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        await fixture.Setup(new SetupHandler.Request("secret", "Root@Example.com", "Ada", "Lovelace"));

        // Then
        var user = await fixture.FindUser("root@example.com");
        user.ShouldNotBeNull().ShouldSatisfyAllConditions(
            entry => entry.Role.ShouldBe(UserRole.Admin),
            entry => entry.Email.ShouldBe("root@example.com"),
            entry => entry.FirstName.ShouldBe("Ada"),
            entry => entry.LastName.ShouldBe("Lovelace"),
            entry => PasswordHasher.Verify("secret", entry.PasswordHash).ShouldBeTrue());
    }

    [Fact]
    public async Task Should_Reject_When_An_Account_Already_Exists()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("existing@example.com", "secret", UserRole.Operator);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Setup(new SetupHandler.Request("secret", "root@example.com")));

        // Then
        exception.ShouldBeOfType<SetupAlreadyCompleteException>();
    }

    [Fact]
    public async Task Should_Throw_On_An_Invalid_Email()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Setup(new SetupHandler.Request("secret", "not-an-email")));

        // Then
        exception.ShouldBeOfType<InvalidEmailException>();
    }
}
