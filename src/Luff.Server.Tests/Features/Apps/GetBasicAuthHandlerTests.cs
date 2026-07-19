using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class GetBasicAuthHandlerTests
{
    [Fact]
    public async Task Should_Return_The_Decrypted_Credential_When_Configured()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();
        await fixture.SetBasicAuth("web", "admin", "s3cret");

        // When
        var result = await fixture.GetBasicAuth("web");

        // Then
        result.ShouldSatisfyAllConditions(
            basicAuth => basicAuth.Configured.ShouldBeTrue(),
            basicAuth => basicAuth.Username.ShouldBe("admin"),
            basicAuth => basicAuth.Password.ShouldBe("s3cret"));
    }

    [Fact]
    public async Task Should_Return_Not_Configured_When_Unset()
    {
        // Given
        using var fixture = new AppsFixture();
        await fixture.HasApp();

        // When
        var result = await fixture.GetBasicAuth("web");

        // Then
        result.ShouldSatisfyAllConditions(
            basicAuth => basicAuth.Configured.ShouldBeFalse(),
            basicAuth => basicAuth.Username.ShouldBeNull(),
            basicAuth => basicAuth.Password.ShouldBeNull());
    }

    [Fact]
    public async Task Should_Throw_When_The_App_Does_Not_Exist()
    {
        // Given
        using var fixture = new AppsFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.GetBasicAuth("ghost"));

        // Then
        exception.ShouldBeOfType<AppNotFoundException>();
    }
}
