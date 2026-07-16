namespace Luff.Server.Features;

public sealed class Registry
{
    public required string Host { get; init; }
    public required string Username { get; set; }
    public required string Password { get; set; }

    public RegistryResponse ToResponse(string password)
    {
        return new RegistryResponse(Host, Username, password);
    }

    public static string? ParseHost(string image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var slash = image.IndexOf('/');
        if (slash < 0)
        {
            return null;
        }

        var prefix = image[..slash];
        if (prefix == "localhost" || prefix.Contains('.') || prefix.Contains(':'))
        {
            return prefix;
        }

        return null;
    }
}
