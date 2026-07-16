using Luff.Server.Features;
using Luff.Server.Tests.Env;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Persistence;

public sealed class AuditInterceptorTests
{
    [Fact]
    public async Task Should_Stamp_CreatedAt_And_UpdatedAt_On_Insert()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");

        // When
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "KEY", "value"));

        // Then
        var stored = (await fixture.GetEnvVars("web")).ShouldHaveSingleItem();
        var now = fixture.Time.GetUtcNow();
        stored.CreatedAt.ShouldBe(now);
        stored.UpdatedAt.ShouldBe(now);
    }

    [Fact]
    public async Task Should_Bump_UpdatedAt_But_Keep_CreatedAt_On_Update()
    {
        // Given
        using var fixture = new EnvFixture();
        await fixture.HasApp("web");
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "KEY", "value"));
        var createdAt = fixture.Time.GetUtcNow();
        fixture.Time.Advance(TimeSpan.FromMinutes(5));

        // When
        await fixture.SetEnv(new SetEnvVarHandler.Request("web", "KEY", "changed"));

        // Then
        var stored = (await fixture.GetEnvVars("web")).ShouldHaveSingleItem();
        stored.CreatedAt.ShouldBe(createdAt);
        stored.UpdatedAt.ShouldBe(createdAt.AddMinutes(5));
    }
}
