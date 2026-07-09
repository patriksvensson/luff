namespace Luff.Server.Features;

public static class NotificationChannels
{
    public static string ValidateName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidNotificationChannelException("The channel name is required");
        }

        return value.Trim();
    }

    public static NotificationChannelType ParseType(string? value)
    {
        if (!Enum.TryParse<NotificationChannelType>(value, ignoreCase: true, out var type) || !Enum.IsDefined(type))
        {
            throw new InvalidNotificationChannelException(
                $"'{value}' is not a valid channel type. Use Discord or generic");
        }

        return type;
    }

    public static string ValidateUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidNotificationChannelException("The webhook URL must be an absolute http(s) URL");
        }

        return value.Trim();
    }
}
