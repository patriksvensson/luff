using System.Buffers.Binary;

namespace Luff.Server.Features;

public static class Totp
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;
    private const int SecretBytes = 20;

    public static string GenerateSecret()
    {
        return Base32Encoder.Encode(RandomNumberGenerator.GetBytes(SecretBytes));
    }

    public static bool Verify(string base32Secret, string code, DateTimeOffset now, int window = 1)
    {
        ArgumentNullException.ThrowIfNull(base32Secret);
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var candidate = code.Trim();
        if (candidate.Length != Digits)
        {
            return false;
        }

        byte[] secret;
        try
        {
            secret = Base32Encoder.Decode(base32Secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var counter = now.ToUnixTimeSeconds() / PeriodSeconds;
        var expected = Encoding.ASCII.GetBytes(candidate);

        var matched = false;
        for (var offset = -window; offset <= window; offset++)
        {
            var step = Encoding.ASCII.GetBytes(Compute(secret, counter + offset));
            matched |= CryptographicOperations.FixedTimeEquals(step, expected);
        }

        return matched;
    }

    public static string Generate(string base32Secret, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(base32Secret);
        return Compute(Base32Encoder.Decode(base32Secret), now.ToUnixTimeSeconds() / PeriodSeconds);
    }

    public static string BuildOtpauthUri(string base32Secret, string username, string issuer = "Luff")
    {
        var label = Uri.EscapeDataString($"{issuer}:{username}");
        var query = $"secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}"
                    + $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";

        return $"otpauth://totp/{label}?{query}";
    }

    private static string Compute(byte[] secret, long counter)
    {
        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, message, hash);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString("D6");
    }
}