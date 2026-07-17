using Luff.Server.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Luff.Server.Tests.Fakes;

// Hands out a LuffDbContext per scope so the AuditLogListener's isolated-write path can be exercised without a
// real DI container.
public sealed class FakeServiceScopeFactory : IServiceScopeFactory
{
    private readonly Func<LuffDbContext> _context;

    public FakeServiceScopeFactory(Func<LuffDbContext> context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IServiceScope CreateScope() => new Scope(_context());

    private sealed class Scope : IServiceScope, IServiceProvider
    {
        private readonly LuffDbContext _context;

        public Scope(LuffDbContext context)
        {
            _context = context;
        }

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(LuffDbContext) ? _context : null;

        public void Dispose() => _context.Dispose();
    }
}
