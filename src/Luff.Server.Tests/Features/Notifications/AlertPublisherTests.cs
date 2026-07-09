using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Notifications;

public sealed class AlertPublisherTests
{
    private const string Webhook = "https://discord.com/api/webhooks/1/2";

    [Fact]
    public async Task Should_Enqueue_A_Delivery_To_Each_Enabled_Channel()
    {
        // Given
        using var fixture = new NotificationsFixture();
        await fixture.AddChannel("team-discord", "discord", Webhook);

        // When
        await fixture.Publish(new Alert(AlertKind.DeployFailed, "Deploy failed: web", "web @ v2 on agent-1: boom"));

        // Then
        var delivery = fixture.Dispatcher.Deliveries.ShouldHaveSingleItem();
        delivery.Url.ShouldBe(Webhook);
        delivery.Body.ShouldBe(
            """
            {
              "embeds": [
                {
                  "title": "\u274C Deploy failed: web",
                  "description": "web @ v2 on agent-1: boom",
                  "color": 15548997,
                  "fields": []
                }
              ]
            }
            """);
    }

    [Fact]
    public async Task Should_Skip_Disabled_Channels()
    {
        // Given
        using var fixture = new NotificationsFixture();
        var channel = await fixture.AddChannel("team-discord", "discord", Webhook);
        await fixture.DisableChannel(channel.Id);

        // When
        await fixture.Publish(new Alert(AlertKind.AgentDisconnected, "Agent disconnected: agent-1", "gone"));

        // Then
        fixture.Dispatcher.Deliveries.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Format_Discord_As_A_Coloured_Embed()
    {
        // Given
        var alert = new Alert(
            AlertKind.AppUnhealthy, "App unhealthy: web", "web on agent-1 reported unhealthy.", "web", "agent-1");

        // When
        var body = NotificationFormat.Build(NotificationChannelType.Discord, alert);

        // Then
        body.ShouldBe(
            """
            {
              "embeds": [
                {
                  "title": "\u26A0\uFE0F App unhealthy: web",
                  "description": "web on agent-1 reported unhealthy.",
                  "color": 15105570,
                  "fields": [
                    {
                      "name": "App",
                      "value": "web",
                      "inline": true
                    },
                    {
                      "name": "Machine",
                      "value": "agent-1",
                      "inline": true
                    }
                  ]
                }
              ]
            }
            """);
    }

    [Fact]
    public void Should_Format_Generic_As_A_Structured_Event()
    {
        // Given
        var alert = new Alert(AlertKind.AppUnhealthy, "App unhealthy: web", "reported unhealthy.", "web", "agent-1");

        // When
        var body = NotificationFormat.Build(NotificationChannelType.Generic, alert);

        // Then
        body.ShouldBe(
            """
            {
              "kind": "AppUnhealthy",
              "title": "App unhealthy: web",
              "message": "reported unhealthy.",
              "app": "web",
              "agent": "agent-1"
            }
            """);
    }
}