using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Luff.Server.Persistence;
using Luff.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace Luff.Server.Tests.Auth;

public sealed class AuthFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LuffDbContext> _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly ISecretProtector _protector;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 06, 30, 12, 0, 0, TimeSpan.Zero));

    public AuthFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LuffDbContext>()
            .UseSqlite(_connection)
            .Options;

        _signingKey = new([.. "0123456789abcdef0123456789abcdef"u8]);
        _protector = new FakeSecretProtector();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public RefreshTokenService CreateRefreshTokenService()
    {
        return new RefreshTokenService(CreateContext(), Time);
    }

    public async Task<LoginResponse> Login(LoginHandler.Request request)
    {
        var handler = new LoginHandler(
            new CredentialVerifier(CreateContext()),
            CreateJwtIssuer(),
            CreateRefreshTokenService(),
            CreateTwoFactorChallenge());

        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<AuthResponse> VerifyTwoFactorLogin(VerifyTwoFactorLoginHandler.Request request)
    {
        var handler = new VerifyTwoFactorLoginHandler(
            CreateTwoFactorChallenge(),
            CreateContext(),
            CreateTwoFactorService(),
            CreateRefreshTokenService(),
            CreateJwtIssuer());

        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task<TwoFactorEnrollmentResponse> BeginTwoFactorEnrollment(string username)
    {
        var handler = new BeginTwoFactorEnrollmentHandler(CreateContext(), _protector);
        return await handler.Handle(new BeginTwoFactorEnrollmentHandler.Request(username), CancellationToken.None);
    }

    public async Task<RecoveryCodesResponse> ConfirmTwoFactorEnrollment(string username, string code)
    {
        var handler =
            new ConfirmTwoFactorEnrollmentHandler(CreateContext(), _protector, CreateRefreshTokenService(), Time);
        return await handler.Handle(
            new ConfirmTwoFactorEnrollmentHandler.Request(username, code), CancellationToken.None);
    }

    public async Task DisableTwoFactor(string username, string code)
    {
        var handler = new DisableTwoFactorHandler(CreateContext(), CreateTwoFactorService(), CreateRefreshTokenService());
        await handler.Handle(new DisableTwoFactorHandler.Request(username, code), CancellationToken.None);
    }

    public async Task ResetUserTwoFactor(string username)
    {
        var handler = new ResetUserTwoFactorHandler(CreateContext(), CreateRefreshTokenService());
        await handler.Handle(new ResetUserTwoFactorHandler.Request(username), CancellationToken.None);
    }

    public async Task Logout(LogoutHandler.Request request)
    {
        var handler = new LogoutHandler(CreateRefreshTokenService());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task ChangePassword(ChangePasswordHandler.Request request)
    {
        var handler = new ChangePasswordHandler(CreateContext(), CreateRefreshTokenService());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task<UserResponse> CreateUser(CreateUserHandler.Request request)
    {
        var handler = new CreateUserHandler(CreateContext());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task Setup(SetupHandler.Request request)
    {
        var handler = new SetupHandler(CreateContext());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task<UserResponse> UpdateUser(UpdateUserHandler.Request request)
    {
        var handler = new UpdateUserHandler(CreateContext(), CreateRefreshTokenService());
        return await handler.Handle(request, CancellationToken.None);
    }

    public async Task DeleteUser(DeleteUserHandler.Request request)
    {
        var handler = new DeleteUserHandler(CreateContext());
        await handler.Handle(request, CancellationToken.None);
    }

    public async Task<IReadOnlyList<UserResponse>> ListUsers()
    {
        var handler = new ListUsersHandler(CreateContext());
        return await handler.Handle(new ListUsersHandler.Request(), CancellationToken.None);
    }

    public async Task HasUser(string username, string password, UserRole role, string? email = null)
    {
        await using var context = CreateContext();

        context.Users.Add(new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            Email = email ?? $"{username}@example.com",
        });

        await context.SaveChangesAsync();
    }

    public async Task HasUserWithTwoFactor(string username, string password, UserRole role, string base32Secret)
    {
        await using var context = CreateContext();

        context.Users.Add(new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            Email = $"{username}@example.com",
            TwoFactorEnabled = true,
            TwoFactorSecret = _protector.Protect(base32Secret),
        });

        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RefreshToken>> GetRefreshTokens(string username)
    {
        await using var context = CreateContext();

        return await context.RefreshTokens
            .Where(token => token.Username == username)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RecoveryCode>> GetRecoveryCodes(string username)
    {
        await using var context = CreateContext();

        return await context.RecoveryCodes
            .Where(code => code.Username == username)
            .ToListAsync();
    }

    public async Task<User?> FindUser(string username)
    {
        await using var context = CreateContext();
        return await context.Users.FindAsync(username);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private TwoFactorService CreateTwoFactorService()
    {
        return new TwoFactorService(CreateContext(), _protector, Time);
    }

    private TwoFactorChallenge CreateTwoFactorChallenge()
    {
        return new TwoFactorChallenge(_signingKey, Time);
    }

    private static FakeJwtIssuer CreateJwtIssuer()
    {
        return new FakeJwtIssuer();
    }

    private LuffDbContext CreateContext()
    {
        return new LuffDbContext(_options);
    }
}