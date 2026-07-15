namespace Luff.Server.Features;

public sealed class CreateUserHandler : IRequestHandler<CreateUserHandler.Request, UserResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<UserResponse>
    {
        public string Username { get; }
        public string Password { get; }
        public string Role { get; }
        public string Email { get; }
        public string? FirstName { get; }
        public string? LastName { get; }

        public Request(
            string username, string password, string role, string email,
            string? firstName = null, string? lastName = null)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            FirstName = firstName;
            LastName = lastName;
        }
    }

    public CreateUserHandler(LuffDbContext database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
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

        var exists = await _database.Users.AnyAsync(user => user.Username == request.Username, cancellationToken);
        if (exists)
        {
            throw new UserAlreadyExistsException(request.Username);
        }

        var emailTaken = await _database.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (emailTaken)
        {
            throw new EmailAlreadyExistsException(email);
        }

        var entity = new User
        {
            Username = request.Username,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = role,
            Email = email,
            FirstName = Clean(request.FirstName),
            LastName = Clean(request.LastName),
        };

        _database.Users.Add(entity);
        await _database.SaveChangesAsync(cancellationToken);

        return entity.ToResponse();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class CreateUserHandlerExtensions
{
    public static async Task<UserResponse> CreateUser(
        this ISender sender, string username, string password, string role, string email,
        string? firstName = null, string? lastName = null, CancellationToken cancellationToken = default)
    {
        return await sender.Send(
            new CreateUserHandler.Request(username, password, role, email, firstName, lastName), cancellationToken);
    }
}
