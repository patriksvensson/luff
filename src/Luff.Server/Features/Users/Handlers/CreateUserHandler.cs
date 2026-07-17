namespace Luff.Server.Features;

public sealed class CreateUserHandler : IRequestHandler<CreateUserHandler.Request, UserResponse>
{
    private readonly LuffDbContext _database;
    private readonly IEventPublisher _events;

    public sealed class Request : IRequest<UserResponse>
    {
        public string Password { get; }
        public string Role { get; }
        public string Email { get; }
        public string Actor { get; }
        public string? FirstName { get; }
        public string? LastName { get; }

        public Request(
            string password, string role, string email, string actor,
            string? firstName = null, string? lastName = null)
        {
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            FirstName = firstName;
            LastName = lastName;
        }
    }

    public CreateUserHandler(LuffDbContext database, IEventPublisher events)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<UserResponse> Handle(Request request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new InvalidUserRoleException(request.Role);
        }

        if (!EmailAddress.TryNormalize(request.Email, out var email))
        {
            throw new InvalidEmailException(request.Email);
        }

        var emailTaken = await _database.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (emailTaken)
        {
            throw new EmailAlreadyExistsException(email);
        }

        var entity = new User
        {
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = role,
            Email = email,
            FirstName = Clean(request.FirstName),
            LastName = Clean(request.LastName),
        };

        _database.Users.Add(entity);
        await _database.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(new AuditEvent
        {
            Kind = AuditEventKind.UserCreated,
            Actor = request.Actor,
            Title = $"User created: {entity.Email}",
            Message = $"{entity.Email} was created as {entity.Role}.",
        }, cancellationToken);

        return entity.ToResponse();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class CreateUserHandlerExtensions
{
    public static async Task<UserResponse> CreateUser(
        this ISender sender, string password, string role, string email, string actor,
        string? firstName = null, string? lastName = null, CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new CreateUserHandler.Request(password, role, email, actor, firstName, lastName), cancellationToken);
    }
}
