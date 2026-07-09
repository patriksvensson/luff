namespace Luff.Server.Features;

public sealed class BeginTwoFactorEnrollmentHandler
    : IRequestHandler<BeginTwoFactorEnrollmentHandler.Request, TwoFactorEnrollmentResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<TwoFactorEnrollmentResponse>
    {
        public string Username { get; }

        public Request(string username)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
        }
    }

    public BeginTwoFactorEnrollmentHandler(LuffDbContext database, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<TwoFactorEnrollmentResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Username], cancellationToken)
            ?? throw new UserNotFoundException(request.Username);

        if (user.TwoFactorEnabled)
        {
            throw new TwoFactorAlreadyEnabledException();
        }

        // Stash the (encrypted) secret now with the flag still off
        // and login ignores it until confirmation.
        var secret = Totp.GenerateSecret();
        user.TwoFactorSecret = _protector.Protect(secret);
        await _database.SaveChangesAsync(cancellationToken);

        var uri = Totp.BuildOtpauthUri(secret, user.Username);
        return new TwoFactorEnrollmentResponse(secret, uri, QrCode.RenderSvg(uri));
    }
}

public static class BeginTwoFactorEnrollmentHandlerExtensions
{
    public static async Task<TwoFactorEnrollmentResponse> BeginTwoFactorEnrollment(
        this ISender sender, string username, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new BeginTwoFactorEnrollmentHandler.Request(username), cancellationToken);
    }
}
