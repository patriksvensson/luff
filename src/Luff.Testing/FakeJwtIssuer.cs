using Luff.Server.Features;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeJwtIssuer : IJwtIssuer
{
    public string Issue(User user)
    {
        return $"access-token-for-{user.Email}";
    }
}
