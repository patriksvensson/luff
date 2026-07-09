using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Infrastructure;

public sealed class ArchitectureTests
{
    [Fact]
    public void Handlers_Should_Not_Depend_On_Senders_Or_Other_Handlers()
    {
        // Given
        var handlers = typeof(DeployEngine).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetInterfaces().Any(IsHandlerContract))
            .ToList();

        // When
        var offenders = handlers
            .Where(handler => handler.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => IsForbidden(parameter.ParameterType)))
            .Select(handler => handler.Name)
            .ToList();

        // Then
        handlers.ShouldNotBeEmpty();
        offenders.ShouldBeEmpty();
    }

    private static bool IsHandlerContract(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
    }

    private static bool IsForbidden(Type type)
    {
        return type == typeof(ISender)
            || type == typeof(IScopedSender)
            || type == typeof(ScopedSender)
            || IsHandlerContract(type)
            || type.GetInterfaces().Any(IsHandlerContract);
    }
}
