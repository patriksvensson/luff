using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Webhooks;

public sealed class CreateWebhookTokenHandlerTests
{
    [Fact]
    public async Task Should_Return_The_Plaintext_Token()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");

        // When
        var result = await fixture.CreateToken(
            new CreateWebhookTokenHandler.Request("web", "github-actions"));

        // Then
        result.Token.ShouldStartWith("luff_");
    }

    [Fact]
    public async Task Should_Store_Only_The_Hash_Of_The_Token()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");

        // When
        var result = await fixture.CreateToken(
            new CreateWebhookTokenHandler.Request("web", "github-actions"));

        // Then
        var stored = await fixture.GetTokens("web");
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            token => token.TokenHash.ShouldBe(WebhookToken.Hash(result.Token)),
            token => token.TokenHash.ShouldNotBe(result.Token));
    }

    [Fact]
    public async Task Should_Store_The_Trimmed_Name()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");

        // When
        var result = await fixture.CreateToken(
            new CreateWebhookTokenHandler.Request("web", "  github-actions  "));

        // Then
        result.Name.ShouldBe("github-actions");
        var stored = await fixture.GetTokens("web");
        stored.ShouldHaveSingleItem().Name.ShouldBe("github-actions");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_The_Name_Is_Missing(string? name)
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateToken(new CreateWebhookTokenHandler.Request("web", name)));

        // Then
        exception.ShouldBeOfType<WebhookTokenNameRequiredException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new WebhooksFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.CreateToken(new CreateWebhookTokenHandler.Request("ghost", "github-actions")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
