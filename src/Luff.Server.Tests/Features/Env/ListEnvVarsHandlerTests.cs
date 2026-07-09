using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Env;

public sealed class ListEnvVarsHandlerTests
{
    [Fact]
    public async Task Should_List_Keys_In_Order()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "DATABASE_URL", "a"));
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "API_KEY", "b"));

        // When
        var result = await fixture.ListEnv(
            new ListEnvVarsHandler.Request("web"));

        // Then
        result.Select(env => env.Key)
            .ShouldBe(["API_KEY", "DATABASE_URL"]);
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new EnvFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.ListEnv(new ListEnvVarsHandler.Request("ghost")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
