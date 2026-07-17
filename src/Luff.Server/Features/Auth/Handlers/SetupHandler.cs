namespace Luff.Server.Features;

public sealed class SetupHandler : IRequestHandler<SetupHandler.Request, Unit>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<Unit>
    {
        public string Password { get; }
        public string Email { get; }
        public string? FirstName { get; }
        public string? LastName { get; }

        public Request(
            string password, string email,
            string? firstName = null, string? lastName = null)
        {
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            FirstName = firstName;
            LastName = lastName;
        }
    }

    public SetupHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryNormalize(request.Email, out var email))
        {
            throw new InvalidEmailException(request.Email);
        }

        // The gate and the insert share a transaction so two racing first-run submits can't both create an admin:
        // the second sees the first's row and is rejected.
        await using var transaction = await _database.Database.BeginTransactionAsync(cancellationToken);

        if (await _database.Users.AnyAsync(cancellationToken))
        {
            throw new SetupAlreadyCompleteException();
        }

        _database.Users.Add(new User
        {
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = UserRole.Admin,
            Email = email,
            FirstName = Clean(request.FirstName),
            LastName = Clean(request.LastName),
        });

        await _database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Unit.Value;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class SetupHandlerExtensions
{
    public static async Task Setup(
        this ISender sender, string password, string email,
        string? firstName = null, string? lastName = null, CancellationToken cancellationToken = default)
    {
        await sender.Send(
            new SetupHandler.Request(password, email, firstName, lastName), cancellationToken);
    }
}
