using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Webhooks;

public sealed class TriggerWebhookHandlerTests
{
    [Fact]
    public async Task Should_Start_A_Deployment_For_The_Tokens_App()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");
        await fixture.HasAttachment("web", "agent-1");
        var token = WebhookToken.Generate();
        await fixture.HasToken("web", token);

        // When
        var result = await fixture.TriggerWebhook(
            new TriggerWebhookHandler.Request(token, "v1"));

        // Then
        result.ShouldSatisfyAllConditions(
            deployment => deployment.App.ShouldBe("web"),
            deployment => deployment.Tag.ShouldBe("v1"),
            deployment => deployment.Status.ShouldBe("InProgress"));
    }

    [Fact]
    public async Task Should_Record_When_The_Token_Was_Last_Used()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");
        var token = WebhookToken.Generate();
        await fixture.HasToken("web", token);

        // When
        await fixture.TriggerWebhook(new TriggerWebhookHandler.Request(token, "v1"));

        // Then
        var stored = await fixture.GetTokens("web");
        stored.ShouldHaveSingleItem()
            .LastUsedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Throw_When_The_Token_Is_Unknown()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.TriggerWebhook(new TriggerWebhookHandler.Request("luff_unknown", "v1")));

        // Then
        exception.ShouldBeOfType<InvalidWebhookTokenException>();
    }

    [Fact]
    public async Task Should_Throw_When_No_Tag_Is_Given()
    {
        // Given
        using var fixture = new WebhooksFixture();
        await fixture.HasApp("web");
        var token = WebhookToken.Generate();
        await fixture.HasToken("web", token);

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.TriggerWebhook(new TriggerWebhookHandler.Request(token, tag: null)));

        // Then
        exception.ShouldBeOfType<WebhookTagRequiredException>();
    }
}
