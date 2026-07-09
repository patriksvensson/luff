using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Webhooks;

public sealed class ListWebhookTokensHandlerTests
{
    [Fact]
    public async Task Should_List_The_Apps_Tokens()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");
        var first = await fixture.HasToken("web", WebhookToken.Generate());
        var second = await fixture.HasToken("web", WebhookToken.Generate());

        // When
        var result = await fixture.ListTokens(
            new ListWebhookTokensHandler.Request("web"));

        // Then
        result.Select(token => token.Id).ShouldBe([first, second], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_Return_The_Token_Name()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");
        await fixture.HasToken("web", WebhookToken.Generate(), "github-actions");

        // When
        var result = await fixture.ListTokens(
            new ListWebhookTokensHandler.Request("web"));

        // Then
        result.ShouldHaveSingleItem().Name.ShouldBe("github-actions");
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new WebhooksFixture();
        var handler = new ListWebhookTokensHandler(fixture.CreateContext());

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ListTokens(new ListWebhookTokensHandler.Request("ghost")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
