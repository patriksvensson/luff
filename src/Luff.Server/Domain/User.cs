namespace Luff.Server.Features;

public sealed class User
{
    public required string Username { get; init; }
    public required string PasswordHash { get; set; }
    public required UserRole Role { get; set; }
    public bool MustChangePassword { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }

    public UserResponse ToResponse()
    {
        return new UserResponse(Username, Role.ToString(), TwoFactorEnabled);
    }
}
