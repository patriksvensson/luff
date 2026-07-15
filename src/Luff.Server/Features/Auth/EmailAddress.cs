using System.Net.Mail;

namespace Luff.Server.Features;

public static class EmailAddress
{
    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (!MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
        {
            return false;
        }

        normalized = parsed.Address.ToLowerInvariant();
        return true;
    }
}
