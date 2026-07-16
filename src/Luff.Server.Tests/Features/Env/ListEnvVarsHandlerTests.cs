using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Env;

public sealed class ListEnvVarsHandlerTests
{
    [Fact]
    public async Task Should_List_Vars_Oldest_First()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "DATABASE_URL", "a"));
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "API_KEY", "b"));

        // When
        var result = await fixture.ListEnv(
            new ListEnvVarsHandler.Request("web"));

        // Then
        result.Select(env => env.Key)
            .ShouldBe(["DATABASE_URL", "API_KEY"]);
    }

    [Fact]
    public async Task Should_Keep_Position_When_A_Var_Is_Updated()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "FIRST", "a"));
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "SECOND", "b"));
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "FIRST", "changed"));

        // When
        var result = await fixture.ListEnv(new ListEnvVarsHandler.Request("web"));

        // Then
        result.Select(env => env.Key)
            .ShouldBe(["FIRST", "SECOND"]);
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

    [Fact]
    public async Task Should_Return_The_Decrypted_Value()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "DATABASE_URL", "postgres://secret"));

        // When
        var result = await fixture.ListEnv(new ListEnvVarsHandler.Request("web"));

        // Then
        result.ShouldHaveSingleItem().Value.ShouldBe("postgres://secret");
    }
}
