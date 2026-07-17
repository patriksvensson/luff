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
        await fixture.HasUser("zoe@example.com", "secret", UserRole.Operator);
        await fixture.HasUser("amy@example.com", "secret", UserRole.Admin);

        // When
        var users = await fixture.ListUsers();

        // Then
        users.Select(user => user.Email).ShouldBe(["amy@example.com", "zoe@example.com"]);
        users[0].Role.ShouldBe("Admin");
    }
}
