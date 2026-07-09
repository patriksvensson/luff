using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Env;

public sealed class UnsetEnvVarHandlerTests
{
    [Fact]
    public async Task Should_Remove_The_Key()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(
            new SetEnvVarHandler.Request(
                "web", "API_KEY", "v"));

        // When
        await fixture.UnsetEnv(
            new UnsetEnvVarHandler.Request(
                "web", "API_KEY"));

        // Then
        (await fixture.GetEnvVars("web")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Throw_When_The_Key_Does_Not_Exist()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.UnsetEnv(
                new UnsetEnvVarHandler.Request(
                    "web", "MISSING")));

        // Then
        exception.ShouldBeOfType<EnvVarNotFoundException>();
    }
}
