using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class ListUsersHandlerTests
{
    [Fact]
    public async Task Should_List_Users_Ordered_By_Name()
    {
        // Given
        using var fixture = new AuthFixture();
        await fixture.HasUser("zoe", "secret", UserRole.Operator);
        await fixture.HasUser("amy", "secret", UserRole.Admin);

        // When
        var users = await fixture.ListUsers();

        // Then
        users.Select(user => user.Username).ShouldBe(["amy", "zoe"]);
        users[0].Role.ShouldBe("Admin");
    }
}
