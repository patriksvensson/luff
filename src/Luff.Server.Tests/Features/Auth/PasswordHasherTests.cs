using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Should_Verify_A_Correct_Password()
    {
        // Given
        var hash = PasswordHasher.Hash("correct horse");

        // When, Then
        PasswordHasher.Verify("correct horse", hash).ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_An_Incorrect_Password()
    {
        // Given
        var hash = PasswordHasher.Hash("correct horse");

        // When, Then
        PasswordHasher.Verify("wrong", hash).ShouldBeFalse();
    }

    [Fact]
    public void Should_Salt_Each_Hash()
    {
        // When
        var first = PasswordHasher.Hash("same");
        var second = PasswordHasher.Hash("same");

        // Then
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Should_Never_Verify_Against_The_Timing_Dummy()
    {
        // Given, When, Then
        PasswordHasher.Verify("anything", PasswordHasher.Dummy).ShouldBeFalse();
    }
}
