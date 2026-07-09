using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Webhooks;

public sealed class WebhookTokenTests
{
    [Fact]
    public void Should_Generate_A_Prefixed_Token()
    {
        // When
        var token = WebhookToken.Generate();

        // Then
        token.ShouldStartWith("luff_");
    }

    [Fact]
    public void Should_Generate_A_Unique_Token_Each_Time()
    {
        // When
        var first = WebhookToken.Generate();
        var second = WebhookToken.Generate();

        // Then
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Should_Hash_The_Same_Token_To_The_Same_Value()
    {
        // Given
        var token = WebhookToken.Generate();

        // When
        var hash = WebhookToken.Hash(token);

        // Then
        hash.ShouldBe(WebhookToken.Hash(token));
    }

    [Fact]
    public void Should_Not_Expose_The_Token_In_Its_Hash()
    {
        // Given
        var token = WebhookToken.Generate();

        // When
        var hash = WebhookToken.Hash(token);

        // Then
        hash.ShouldNotBe(token);
    }
}
