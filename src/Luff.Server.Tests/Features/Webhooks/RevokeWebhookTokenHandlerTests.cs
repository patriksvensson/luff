using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Webhooks;

public sealed class RevokeWebhookTokenHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Token()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");
        var id = await fixture.HasToken("web", WebhookToken.Generate());

        // When
        await fixture.RevokeToken(
            new RevokeWebhookTokenHandler.Request("web", id));

        // Then
        (await fixture.GetTokens("web")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_When_The_Token_Does_Not_Exist()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.RevokeToken(
                new RevokeWebhookTokenHandler.Request(
                    "web", Guid.NewGuid())));

        // Then
        exception.ShouldBeOfType<WebhookTokenNotFoundException>();
    }
}
