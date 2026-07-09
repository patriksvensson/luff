using System.Text.Json;

namespace Luff.Server.Features;

public static class NotificationFormat
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
    };

    public static string Build(NotificationChannelType type, Alert alert)
    {
        return type switch
        {
            NotificationChannelType.Discord => BuildDiscord(alert),
            _ => BuildGeneric(alert),
        };
    }

    // Discord renders an embed with a coloured left bar, so each kind carries a colour and a leading icon:
    // green for good news, red for failures, amber for a warning, grey for a neutral state change. App and
    // machine ride along as inline fields, the Coolify-style structured look.
    private static string BuildDiscord(Alert alert)
    {
        var (icon, color) = Style(alert.Kind);

        var fields = new List<object>();
        if (!string.IsNullOrEmpty(alert.App))
        {
            fields.Add(new { name = "App", value = alert.App, inline = true });
        }

        if (!string.IsNullOrEmpty(alert.Agent))
        {
            fields.Add(new { name = "Machine", value = alert.Agent, inline = true });
        }

        var embed = new
        {
            title = $"{icon} {alert.Title}",
            description = alert.Message,
            color,
            fields,
        };

        return JsonSerializer.Serialize(
            new { embeds = new[] { embed } },
            _options);
    }

    private static string BuildGeneric(Alert alert)
    {
        return JsonSerializer.Serialize(new
        {
            kind = alert.Kind.ToString(),
            title = alert.Title,
            message = alert.Message,
            app = alert.App,
            agent = alert.Agent,
        }, _options);
    }

    private static (string Icon, int Color) Style(AlertKind kind)
    {
        return kind switch
        {
            AlertKind.DeploySucceeded => ("✅", 0x2ECC71),
            AlertKind.DeployFailed => ("❌", 0xED4245),
            AlertKind.AppUnhealthy => ("⚠️", 0xE67E22),
            AlertKind.AgentConnected => ("\U0001F7E2", 0x2ECC71),
            AlertKind.AgentDisconnected => ("\U0001F534", 0xED4245),
            AlertKind.AppStarted => ("▶️", 0x2ECC71),
            AlertKind.AppStopped => ("⏹️", 0x95A5A6),
            _ => ("\U0001F514", 0x95A5A6),
        };
    }
}
