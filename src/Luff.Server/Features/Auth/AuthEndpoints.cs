namespace Luff.Server.Features;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group
            .MapGroup("/auth")
            .WithTags("Auth");

        auth.MapPost("/login", Login)
            .WithName("Auth_Login")
            .AllowAnonymous()
            .RequireRateLimiting(JwtAuth.CredentialsPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/login/2fa", VerifyTwoFactor)
            .WithName("Auth_VerifyTwoFactor")
            .AllowAnonymous()
            .RequireRateLimiting(JwtAuth.CredentialsPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/refresh", Refresh)
            .WithName("Auth_Refresh")
            .AllowAnonymous()
            .RequireRateLimiting(JwtAuth.CredentialsPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", Logout)
            .WithName("Auth_Logout")
            .AllowAnonymous();

        auth.MapPost("/password", ChangePassword)
            .WithName("Auth_Password")
            .RequireRateLimiting(JwtAuth.CredentialsPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/2fa", TwoFactorStatus)
            .WithName("Auth_TwoFactorStatus");

        auth.MapPost("/2fa/enroll", BeginTwoFactorEnrollment)
            .WithName("Auth_BeginTwoFactorEnrollment")
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/2fa/confirm", ConfirmTwoFactor)
            .WithName("Auth_ConfirmTwoFactor")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/2fa/disable", DisableTwoFactor)
            .WithName("Auth_DisableTwoFactor")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/2fa/recovery-codes", RegenerateRecoveryCodes)
            .WithName("Auth_RegenerateRecoveryCodes")
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Ok<LoginResponse>> Login(
        LoginRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.Login(request.Username, request.Password, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<AuthResponse>> VerifyTwoFactor(
        VerifyTwoFactorRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var tokens = await sender.VerifyTwoFactorLogin(request.ChallengeToken, request.Code, cancellationToken);
        return TypedResults.Ok(tokens);
    }

    private static async Task<Ok<AuthResponse>> Refresh(
        RefreshRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var tokens = await sender.Refresh(request.RefreshToken, cancellationToken);
        return TypedResults.Ok(tokens);
    }

    private static async Task<NoContent> Logout(
        LogoutRequest request, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Logout(request.RefreshToken, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ChangePassword(
        ChangePasswordRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name ?? throw new InvalidCredentialsException();
        await sender.ChangePassword(username, request.CurrentPassword, request.NewPassword, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<TwoFactorStatusResponse>> TwoFactorStatus(
        ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name ?? throw new InvalidCredentialsException();
        var status = await sender.GetTwoFactorStatus(username, cancellationToken);
        return TypedResults.Ok(status);
    }

    private static async Task<Ok<TwoFactorEnrollmentResponse>> BeginTwoFactorEnrollment(
        ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name ?? throw new InvalidCredentialsException();
        var enrollment = await sender.BeginTwoFactorEnrollment(username, cancellationToken);
        return TypedResults.Ok(enrollment);
    }

    private static async Task<Ok<RecoveryCodesResponse>> ConfirmTwoFactor(
        ConfirmTwoFactorRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name ?? throw new InvalidCredentialsException();
        var codes = await sender.ConfirmTwoFactorEnrollment(username, request.Code, cancellationToken);
        return TypedResults.Ok(codes);
    }

    private static async Task<NoContent> DisableTwoFactor(
        DisableTwoFactorRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name ?? throw new InvalidCredentialsException();
        await sender.DisableTwoFactor(username, request.Code, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<RecoveryCodesResponse>> RegenerateRecoveryCodes(
        ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name ?? throw new InvalidCredentialsException();
        var codes = await sender.RegenerateRecoveryCodes(username, cancellationToken);
        return TypedResults.Ok(codes);
    }
}
