using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Env;

public sealed class SetEnvVarHandlerTests
{
    [Fact]
    public async Task Should_Store_The_Value_Encrypted()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");

        // When
        await fixture.SetEnv(
            new SetEnvVarHandler.Request(
                "web", "API_KEY", "secret"));

        // Then
        var stored = await fixture.GetEnvVars("web");
        stored.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            env => env.Key.ShouldBe("API_KEY"),
            env => env.Value.ShouldBe(fixture.Protector.Protect("secret")),
            env => env.Value.ShouldNotBe("secret"));
    }

    [Fact]
    public async Task Should_Update_An_Existing_Key()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(
            new SetEnvVarHandler.Request(
                "web", "API_KEY", "old"));

        // When
        await fixture.SetEnv(
            new SetEnvVarHandler.Request(
                "web", "API_KEY", "new"));

        // Then
        var stored = await fixture.GetEnvVars("web");
        stored.ShouldHaveSingleItem().Value.ShouldBe(fixture.Protector.Protect("new"));
    }

    [Fact]
    public async Task Should_Throw_When_The_Key_Is_Invalid()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.SetEnv(
                new SetEnvVarHandler.Request("web", "not a key", "v")));

        // Then
        exception.ShouldBeOfType<InvalidEnvKeyException>();
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new EnvFixture();

        // When
        var exception = await Record.ExceptionAsync(() =>
            fixture.SetEnv(
                new SetEnvVarHandler.Request(
                    "ghost", "API_KEY", "v")));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
