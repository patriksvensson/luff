namespace Luff.Server.Features;

public sealed class CreateUserHandler : IRequestHandler<CreateUserHandler.Request, UserResponse>
{
    private readonly LuffDbContext _database;

    public sealed class Request : IRequest<UserResponse>
    {
        public string Username { get; }
        public string Password { get; }
        public string Role { get; }

        public Request(string username, string password, string role)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Role = role ?? throw new ArgumentNullException(nameof(role));
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

        var exists = await _database.Users.AnyAsync(user => user.Username == request.Username, cancellationToken);
        if (exists)
        {
            throw new UserAlreadyExistsException(request.Username);
        }

        var entity = new User
        {
            Username = request.Username,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = role,
        };

        _database.Users.Add(entity);
        await _database.SaveChangesAsync(cancellationToken);

        return entity.ToResponse();
    }
}

public static class CreateUserHandlerExtensions
{
    public static async Task<UserResponse> CreateUser(
        this ISender sender, string username, string password, string role,
        CancellationToken cancellationToken = default)
    {
        return await sender.Send(new CreateUserHandler.Request(username, password, role), cancellationToken);
    }
}
