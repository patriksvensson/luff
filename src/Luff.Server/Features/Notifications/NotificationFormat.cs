using System.Text.Json;

namespace Luff.Server.Features;

public static class NotificationFormat
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
    };

    public static string Build(NotificationChannelType type, AuditEvent auditEvent)
    {
        return type switch
        {
            NotificationChannelType.Discord => BuildDiscord(auditEvent),
            _ => BuildGeneric(auditEvent),
        };
    }

    // Discord renders an embed with a coloured left bar, so each kind carries a colour and a leading icon:
    // green for good news, red for failures, amber for a warning, grey for a neutral state change. App and
    // machine ride along as inline fields, the Coolify-style structured look.
    private static string BuildDiscord(AuditEvent auditEvent)
    {
        var (icon, color) = Style(auditEvent.Kind);

        var fields = new List<object>();
        if (!string.IsNullOrEmpty(auditEvent.App))
        {
            fields.Add(new { name = "App", value = auditEvent.App, inline = true });
        }

        if (!string.IsNullOrEmpty(auditEvent.Agent))
        {
            fields.Add(new { name = "Machine", value = auditEvent.Agent, inline = true });
        }

        var embed = new
        {
            title = $"{icon} {auditEvent.Title}",
            description = auditEvent.Message,
            color,
            fields,
        };

        return JsonSerializer.Serialize(
            new { embeds = new[] { embed } },
            _options);
    }

    private static string BuildGeneric(AuditEvent auditEvent)
    {
        return JsonSerializer.Serialize(new
        {
            kind = auditEvent.Kind.ToString(),
            title = auditEvent.Title,
            message = auditEvent.Message,
            app = auditEvent.App,
            agent = auditEvent.Agent,
        }, _options);
    }

    private static (string Icon, int Color) Style(AuditEventKind kind)
    {
        return kind switch
        {
            AuditEventKind.DeploySucceeded => ("✅", 0x2ECC71),
            AuditEventKind.DeployFailed => ("❌", 0xED4245),
            AuditEventKind.AppUnhealthy => ("⚠️", 0xE67E22),
            AuditEventKind.AgentConnected => ("\U0001F7E2", 0x2ECC71),
            AuditEventKind.AgentDisconnected => ("\U0001F534", 0xED4245),
            AuditEventKind.AppCreated => ("\U00002728", 0x95A5A6),
            AuditEventKind.AppUpdated => ("\U0000270F\U0000FE0F", 0x95A5A6),
            AuditEventKind.AppDeleted => ("\U0001F5D1\U0000FE0F", 0x95A5A6),
            AuditEventKind.AppStarted => ("▶️", 0x2ECC71),
            AuditEventKind.AppStopped => ("⏹️", 0x95A5A6),
            AuditEventKind.AppStartFailed => ("❌", 0xED4245),
            AuditEventKind.AppStopFailed => ("❌", 0xED4245),
            AuditEventKind.AgentEnrolled => ("\U0001F5A5️", 0x95A5A6),
            AuditEventKind.AgentRemoved => ("\U0001F5A5️", 0x95A5A6),
            AuditEventKind.RegistryAdded => ("\U0001F4E6", 0x95A5A6),
            AuditEventKind.RegistryRemoved => ("\U0001F4E6", 0x95A5A6),
            AuditEventKind.VolumeAdded => ("\U0001F4BE", 0x95A5A6),
            AuditEventKind.VolumeRemoved => ("\U0001F4BE", 0x95A5A6),
            AuditEventKind.UserCreated => ("\U0001F464", 0x95A5A6),
            AuditEventKind.UserDeleted => ("\U0001F464", 0x95A5A6),
            AuditEventKind.TwoFactorEnabled => ("\U0001F510", 0x2ECC71),
            AuditEventKind.TwoFactorDisabled => ("\U0001F513", 0xE67E22),
            AuditEventKind.ServerStarted => ("\U0001F680", 0x2ECC71),
            _ => ("\U0001F514", 0x95A5A6),
        };
    }
}
