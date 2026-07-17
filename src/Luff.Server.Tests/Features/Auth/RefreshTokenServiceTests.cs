using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task Should_Issue_An_Active_Token()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();

        // When
        var token = await service.IssueAsync("admin@example.com", CancellationToken.None);

        // Then
        var stored = await fixture.GetRefreshTokens("admin@example.com");
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            entry => entry.TokenHash.ShouldBe(RefreshToken.Hash(token)),
            entry => entry.ConsumedAt.ShouldBeNull(),
            entry => entry.RevokedAt.ShouldBeNull());
    }

    [Fact]
    public async Task Should_Rotate_Consuming_The_Old_Token()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();
        var first = await service.IssueAsync("admin@example.com", CancellationToken.None);

        // When
        var (second, email) = await service.RotateAsync(first, CancellationToken.None);

        // Then
        email.ShouldBe("admin@example.com");
        second.ShouldNotBe(first);
        var stored = await fixture.GetRefreshTokens("admin@example.com");
        stored.Single(entry => entry.TokenHash == RefreshToken.Hash(first)).ConsumedAt.ShouldNotBeNull();
        stored.Single(entry => entry.TokenHash == RefreshToken.Hash(second)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Keep_The_Absolute_Expiry_On_Rotation()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();
        var first = await service.IssueAsync("admin@example.com", CancellationToken.None);
        var expiry = (await fixture.GetRefreshTokens("admin@example.com")).Single().ExpiresAt;

        // When
        fixture.Time.Advance(TimeSpan.FromDays(5));
        var (second, _) = await service.RotateAsync(first, CancellationToken.None);

        // Then
        (await fixture.GetRefreshTokens("admin@example.com"))
            .Single(entry => entry.TokenHash == RefreshToken.Hash(second))
            .ExpiresAt.ShouldBe(expiry);
    }

    [Fact]
    public async Task Should_Revoke_The_Family_When_A_Consumed_Token_Is_Reused()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();
        var first = await service.IssueAsync("admin@example.com", CancellationToken.None);
        await service.RotateAsync(first, CancellationToken.None);

        // When
        var exception = await Record.ExceptionAsync(() => service.RotateAsync(first, CancellationToken.None));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
        (await fixture.GetRefreshTokens("admin@example.com")).ShouldAllBe(entry => entry.RevokedAt != null);
    }

    [Fact]
    public async Task Should_Reject_An_Expired_Token()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();
        var token = await service.IssueAsync("admin@example.com", CancellationToken.None);

        // When
        fixture.Time.Advance(RefreshTokenService.Lifetime + TimeSpan.FromMinutes(1));
        var exception = await Record.ExceptionAsync(() => service.RotateAsync(token, CancellationToken.None));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Should_Reject_An_Unknown_Token()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();

        // When
        var exception = await Record.ExceptionAsync(() => service.RotateAsync("luff_unknown", CancellationToken.None));

        // Then
        exception.ShouldBeOfType<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Should_Revoke_All_The_Users_Tokens()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("admin@example.com", "secret", UserRole.Admin);
        var service = fixture.CreateRefreshTokenService();
        await service.IssueAsync("admin@example.com", CancellationToken.None);
        await service.IssueAsync("admin@example.com", CancellationToken.None);

        // When
        await service.RevokeAllAsync("admin@example.com", CancellationToken.None);

        // Then
        (await fixture.GetRefreshTokens("admin@example.com")).ShouldAllBe(entry => entry.RevokedAt != null);
    }
}
