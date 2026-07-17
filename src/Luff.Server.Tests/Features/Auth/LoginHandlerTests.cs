using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class LoginHandlerTests
{
    [Fact]
    public async Task Should_Issue_Tokens_For_Valid_Credentials()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);

        // When
        var result = await fixture.Login(
            new LoginHandler.Request("admin@example.com", "secret"));

        // Then
        result.TwoFactorRequired.ShouldBeFalse();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNull().ShouldStartWith("luff_");
    }

    [Fact]
    public async Task Should_Reject_A_Wrong_Password()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Login(new LoginHandler.Request("admin@example.com", "wrong")));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Should_Reject_An_Unknown_User()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.Login(new LoginHandler.Request("ghost@example.com", "secret")));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
    }
}
