namespace Luff.Server.Features;

public sealed class LoginRequest
{
    public string Username { get; }
    public string Password { get; }

    public LoginRequest(string username, string password)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; }

    public RefreshRequest(string refreshToken)
    {
        RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
    }
}

public sealed class LogoutRequest
{
    public string RefreshToken { get; }

    public LogoutRequest(string refreshToken)
    {
        RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
    }
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; }
    public string NewPassword { get; }

    public ChangePasswordRequest(string currentPassword, string newPassword)
    {
        CurrentPassword = currentPassword ?? throw new ArgumentNullException(nameof(currentPassword));
        NewPassword = newPassword ?? throw new ArgumentNullException(nameof(newPassword));
    }
}

public sealed class CreateUserRequest
{
    public string Username { get; }
    public string Password { get; }
    public string Role { get; }

    public CreateUserRequest(string username, string password, string role)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        Role = role ?? throw new ArgumentNullException(nameof(role));
    }
}

public sealed class AuthResponse
{
    public string AccessToken { get; }
    public string RefreshToken { get; }

    public AuthResponse(string accessToken, string refreshToken)
    {
        AccessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
    }
}

public sealed class UserResponse
{
    public string Username { get; }
    public string Role { get; }
    public bool TwoFactorEnabled { get; }

    public UserResponse(string username, string role, bool twoFactorEnabled)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        TwoFactorEnabled = twoFactorEnabled;
    }
}

public sealed class LoginResponse
{
    public bool TwoFactorRequired { get; }
    public string? AccessToken { get; }
    public string? RefreshToken { get; }
    public string? ChallengeToken { get; }

    private LoginResponse(
        bool twoFactorRequired, string? accessToken,
        string? refreshToken, string? challengeToken)
    {
        TwoFactorRequired = twoFactorRequired;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ChallengeToken = challengeToken;
    }

    public static LoginResponse Tokens(AuthResponse tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new LoginResponse(false, tokens.AccessToken, tokens.RefreshToken, null);
    }

    public static LoginResponse Challenge(string challengeToken)
    {
        ArgumentNullException.ThrowIfNull(challengeToken);
        return new LoginResponse(true, null, null, challengeToken);
    }
}

public sealed class VerifyTwoFactorRequest
{
    public string ChallengeToken { get; }
    public string Code { get; }

    public VerifyTwoFactorRequest(string challengeToken, string code)
    {
        ChallengeToken = challengeToken ?? throw new ArgumentNullException(nameof(challengeToken));
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }
}

public sealed class ConfirmTwoFactorRequest
{
    public string Code { get; }

    public ConfirmTwoFactorRequest(string code)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }
}

public sealed class DisableTwoFactorRequest
{
    public string Code { get; }

    public DisableTwoFactorRequest(string code)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }
}

public sealed class TwoFactorEnrollmentResponse
{
    public string Secret { get; }
    public string OtpauthUri { get; }
    public string QrSvg { get; }

    public TwoFactorEnrollmentResponse(string secret, string otpauthUri, string qrSvg)
    {
        Secret = secret ?? throw new ArgumentNullException(nameof(secret));
        OtpauthUri = otpauthUri ?? throw new ArgumentNullException(nameof(otpauthUri));
        QrSvg = qrSvg ?? throw new ArgumentNullException(nameof(qrSvg));
    }
}

public sealed class RecoveryCodesResponse
{
    public IReadOnlyList<string> Codes { get; }

    public RecoveryCodesResponse(IReadOnlyList<string> codes)
    {
        Codes = codes ?? throw new ArgumentNullException(nameof(codes));
    }
}

public sealed class TwoFactorStatusResponse
{
    public bool Enabled { get; }
    public int RemainingRecoveryCodes { get; }

    public TwoFactorStatusResponse(bool enabled, int remainingRecoveryCodes)
    {
        Enabled = enabled;
        RemainingRecoveryCodes = remainingRecoveryCodes;
    }
}
