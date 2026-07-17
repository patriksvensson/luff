using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class TwoFactorHandlerTests
{
    [Fact]
    public async Task Should_Stash_A_Secret_On_Enrollment_Without_Enabling()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);

        // When
        var enrollment = await fixture.BeginTwoFactorEnrollment("admin@example.com");

        // Then
        enrollment.Secret.ShouldNotBeNullOrEmpty();
        enrollment.QrSvg.ShouldContain("<svg");
        var user = await fixture.FindUser("admin@example.com");
        user.ShouldNotBeNull();
        user.TwoFactorEnabled.ShouldBeFalse();
        user.TwoFactorSecret.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Enable_And_Return_Ten_Backup_Codes_On_Confirm()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var enrollment = await fixture.BeginTwoFactorEnrollment("admin@example.com");
        var code = Totp.Generate(enrollment.Secret, fixture.Time.GetUtcNow());

        // When
        var result = await fixture.ConfirmTwoFactorEnrollment("admin@example.com", code);

        // Then
        result.Codes.Count.ShouldBe(10);
        (await fixture.FindUser("admin@example.com")).ShouldNotBeNull()
            .TwoFactorEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Reject_Confirmation_With_A_Wrong_Code()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        await fixture.BeginTwoFactorEnrollment("admin@example.com");

        // When
        var exception = await Record.ExceptionAsync(() => fixture.ConfirmTwoFactorEnrollment("admin@example.com", "000000"));

        // Then
        exception.ShouldBeOfType<InvalidTwoFactorCodeException>();
    }

    [Fact]
    public async Task Should_Return_A_Challenge_Instead_Of_Tokens_When_Two_Factor_Is_On()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("admin@example.com", "secret", UserRole.Admin, secret);

        // When
        var result = await fixture.Login(
            new LoginHandler.Request("admin@example.com", "secret"));

        // Then
        result.TwoFactorRequired.ShouldBeTrue();
        result.AccessToken.ShouldBeNull();
        result.ChallengeToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Require_Two_Factor_At_Login_After_Enrolling_Through_The_Handlers()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var enrollment = await fixture.BeginTwoFactorEnrollment("admin@example.com");
        await fixture.ConfirmTwoFactorEnrollment(
            "admin@example.com", Totp.Generate(enrollment.Secret, fixture.Time.GetUtcNow()));

        // When
        var login = await fixture.Login(
            new LoginHandler.Request("admin@example.com", "secret"));

        // Then
        login.TwoFactorRequired.ShouldBeTrue();
        login.AccessToken.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Exchange_A_Challenge_And_Code_For_Tokens()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("admin@example.com", "secret", UserRole.Admin, secret);
        var login = await fixture.Login(new LoginHandler.Request("admin@example.com", "secret"));
        var code = Totp.Generate(secret, fixture.Time.GetUtcNow());

        // When
        var tokens = await fixture.VerifyTwoFactorLogin(
            new VerifyTwoFactorLoginHandler.Request(
                login.ChallengeToken!, code));

        // Then
        tokens.AccessToken.ShouldNotBeNullOrEmpty();
        tokens.RefreshToken.ShouldStartWith("luff_");
    }

    [Fact]
    public async Task Should_Reject_A_Wrong_Login_Code()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("admin@example.com", "secret", UserRole.Admin, secret);
        var login = await fixture.Login(new LoginHandler.Request("admin@example.com", "secret"));

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.VerifyTwoFactorLogin(
                new VerifyTwoFactorLoginHandler.Request(
                    login.ChallengeToken!, "000000")));

        // Then
        exception.ShouldBeOfType<InvalidTwoFactorCodeException>();
    }

    [Fact]
    public async Task Should_Reject_A_Garbled_Challenge()
    {
        // Given
        using var fixture = new AuthFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.VerifyTwoFactorLogin(
                new VerifyTwoFactorLoginHandler.Request(
                    "not-a-real-token", "000000")));

        // Then
        exception.ShouldBeOfType<TwoFactorChallengeInvalidException>();
    }

    [Fact]
    public async Task Should_Consume_A_Backup_Code_Once()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var enrollment = await fixture.BeginTwoFactorEnrollment("admin@example.com");
        var setup = await fixture.ConfirmTwoFactorEnrollment(
            "admin@example.com", Totp.Generate(enrollment.Secret, fixture.Time.GetUtcNow()));
        var backup = setup.Codes[0];

        var first = await fixture.Login(new LoginHandler.Request("admin@example.com", "secret"));
        await fixture.VerifyTwoFactorLogin(new VerifyTwoFactorLoginHandler.Request(first.ChallengeToken!, backup));

        // When
        var second = await fixture.Login(new LoginHandler.Request("admin@example.com", "secret"));
        var exception = await Record.ExceptionAsync(() =>
            fixture.VerifyTwoFactorLogin(new VerifyTwoFactorLoginHandler.Request(second.ChallengeToken!, backup)));

        // Then
        exception.ShouldBeOfType<InvalidTwoFactorCodeException>();
    }

    [Fact]
    public async Task Should_Disable_With_A_Valid_Code()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("admin@example.com", "secret", UserRole.Admin, secret);

        // When
        await fixture.DisableTwoFactor(
            "admin@example.com", Totp.Generate(secret, fixture.Time.GetUtcNow()));

        // Then
        var user = await fixture.FindUser("admin@example.com");
        user.ShouldNotBeNull();
        user.TwoFactorEnabled.ShouldBeFalse();
        user.TwoFactorSecret.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reset_A_Users_Two_Factor_As_Admin()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("bob@example.com", "secret", UserRole.Operator, secret);

        // When
        await fixture.ResetUserTwoFactor("bob@example.com");

        // Then
        var user = await fixture.FindUser("bob@example.com");
        user.ShouldNotBeNull();
        user.TwoFactorEnabled.ShouldBeFalse();
        (await fixture.GetRecoveryCodes("bob@example.com")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Publish_A_Two_Factor_Enabled_Event_On_Confirm()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var enrollment = await fixture.BeginTwoFactorEnrollment("admin@example.com");

        // When
        await fixture.ConfirmTwoFactorEnrollment(
            "admin@example.com", Totp.Generate(enrollment.Secret, fixture.Time.GetUtcNow()));

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.TwoFactorEnabled),
            evt => evt.Actor.ShouldBe("admin@example.com"));
    }

    [Fact]
    public async Task Should_Publish_A_Two_Factor_Disabled_Event_On_Self_Disable()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("admin@example.com", "secret", UserRole.Admin, secret);

        // When
        await fixture.DisableTwoFactor("admin@example.com", Totp.Generate(secret, fixture.Time.GetUtcNow()));

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.TwoFactorDisabled),
            evt => evt.Actor.ShouldBe("admin@example.com"));
    }

    [Fact]
    public async Task Should_Attribute_A_Two_Factor_Reset_To_The_Admin()
    {
        // Given
        using var fixture = new AuthFixture();
        var secret = Totp.GenerateSecret();
        await fixture.HasUserWithTwoFactor("bob@example.com", "secret", UserRole.Operator, secret);

        // When
        await fixture.ResetUserTwoFactor("bob@example.com", actor: "admin@example.com");

        // Then
        fixture.Events.Published.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            evt => evt.Kind.ShouldBe(AuditEventKind.TwoFactorDisabled),
            evt => evt.Actor.ShouldBe("admin@example.com"),
            evt => evt.Title.ShouldContain("bob@example.com"));
    }
}