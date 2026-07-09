namespace Luff.Server.Features;

public interface IJwtIssuer
{
    string Issue(User user);
}
