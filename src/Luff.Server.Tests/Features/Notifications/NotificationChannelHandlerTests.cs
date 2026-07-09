using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Notifications;

public sealed class NotificationChannelHandlerTests
{
    private const string Webhook = "https://discord.com/api/webhooks/123/abc";

    [Fact]
    public async Task Should_Add_A_Channel_And_Not_Return_The_Url()
    {
        // Given
        using var fixture = new NotificationsFixture();

        // When
        var result = await fixture.AddChannel("team-discord", "discord", Webhook);

        // Then
        result.ShouldSatisfyAllConditions(
            channel => channel.Name.ShouldBe("team-discord"),
            channel => channel.Type.ShouldBe("Discord"),
            channel => channel.Enabled.ShouldBeTrue());
    }

    [Fact]
    public async Task Should_Store_The_Url_Encrypted()
    {
        // Given
        using var fixture = new NotificationsFixture();

        // When
        await fixture.AddChannel("team-discord", "discord", Webhook);

        // Then
        await using var context = fixture.CreateContext();
        var stored = context.NotificationChannels.Single();
        stored.Url.ShouldBe($"protected:{Webhook}");
    }

    [Fact]
    public async Task Should_Reject_An_Unknown_Type()
    {
        // Given
        using var fixture = new NotificationsFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.AddChannel("x", "telegram", "https://example.com/hook"));

        // Then
        exception.ShouldBeOfType<InvalidNotificationChannelException>();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    public async Task Should_Reject_A_Bad_Url(string url)
    {
        // Given
        using var fixture = new NotificationsFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.AddChannel("x", "discord", url));

        // Then
        exception.ShouldBeOfType<InvalidNotificationChannelException>();
    }

    [Fact]
    public async Task Should_Remove_A_Channel()
    {
        // Given
        using var fixture = new NotificationsFixture();
        var channel = await fixture.AddChannel("x", "generic", Webhook);

        // When
        await fixture.RemoveChannel(channel.Id);

        // Then
        (await fixture.ListChannels()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_When_Removing_A_Missing_Channel()
    {
        // Given
        using var fixture = new NotificationsFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.RemoveChannel(Guid.NewGuid()));

        // Then
        exception.ShouldBeOfType<NotificationChannelNotFoundException>();
    }

    [Fact]
    public async Task Should_Enqueue_A_Test_Delivery_Even_When_Disabled()
    {
        // Given
        using var fixture = new NotificationsFixture();
        var channel = await fixture.AddChannel("x", "discord", Webhook);
        await fixture.DisableChannel(channel.Id);

        // When
        await fixture.TestChannel(channel.Id);

        // Then
        fixture.Dispatcher.Deliveries.ShouldHaveSingleItem()
            .Url.ShouldBe(Webhook);
    }
}
