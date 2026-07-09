namespace Luff.Server.Features;

public sealed class AgentEnrollmentValidator
{
    private readonly byte[] _expected;

    public AgentEnrollmentValidator(IOptions<AgentEnrollmentOptions> options)
    {
        _expected = Encoding.UTF8.GetBytes(options.Value.Secret ?? string.Empty);
    }

    public bool IsValid(string? presented)
    {
        if (_expected.Length == 0)
        {
            return false;
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presented ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(presentedBytes, _expected);
    }
}
