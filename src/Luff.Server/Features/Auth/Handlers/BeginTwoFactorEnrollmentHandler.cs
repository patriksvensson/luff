namespace Luff.Server.Features;

public sealed class BeginTwoFactorEnrollmentHandler
    : IRequestHandler<BeginTwoFactorEnrollmentHandler.Request, TwoFactorEnrollmentResponse>
{
    private readonly LuffDbContext _database;
    private readonly ISecretProtector _protector;

    public sealed class Request : IRequest<TwoFactorEnrollmentResponse>
    {
        public string Email { get; }

        public Request(string email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }

    public BeginTwoFactorEnrollmentHandler(LuffDbContext database, ISecretProtector protector)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<TwoFactorEnrollmentResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync([request.Email], cancellationToken)
            ?? throw new UserNotFoundException(request.Email);

        if (user.TwoFactorEnabled)
        {
            throw new TwoFactorAlreadyEnabledException();
        }

        // Stash the (encrypted) secret now with the flag still off
        // and login ignores it until confirmation.
        var secret = Totp.GenerateSecret();
        user.TwoFactorSecret = _protector.Protect(secret);
        await _database.SaveChangesAsync(cancellationToken);

        var uri = Totp.BuildOtpauthUri(secret, user.Email);
        return new TwoFactorEnrollmentResponse(secret, uri, QrCode.RenderSvg(uri));
    }
}

public static class BeginTwoFactorEnrollmentHandlerExtensions
{
    public static async Task<TwoFactorEnrollmentResponse> BeginTwoFactorEnrollment(
        this ISender sender, string email, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new BeginTwoFactorEnrollmentHandler.Request(email), cancellationToken);
    }
}
