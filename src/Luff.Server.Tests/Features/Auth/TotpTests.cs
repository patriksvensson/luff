using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Auth;

public sealed class TotpTests
{
    // RFC 6238 Appendix B uses the ASCII seed "12345678901234567890".
    // The codes below are the 6-digit truncation of its published 8-digit SHA-1 values.
    private static readonly string _secret = Base32Encoder.Encode("12345678901234567890"u8);

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Should_Accept_The_Rfc6238_Reference_Codes(long unixSeconds, string code)
    {
        // Given
        var now = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        // When
        var result = Totp.Verify(_secret, code, now, window: 0);

        // Then
        result.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_A_Wrong_Code()
    {
        // Given
        var now = DateTimeOffset.FromUnixTimeSeconds(59);

        // When
        var result = Totp.Verify(_secret, "000000", now, window: 0);

        // Then
        result.ShouldBeFalse();
    }

    [Fact]
    public void Should_Accept_A_Code_From_The_Adjacent_Window()
    {
        // Given
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var previousStep = Totp.Generate(_secret, now.AddSeconds(-30));

        // When
        var result = Totp.Verify(_secret, previousStep, now, window: 1);

        // Then
        result.ShouldBeTrue();
    }

    [Fact]
    public void Should_Round_Trip_Base32()
    {
        // Given
        var data = "12345678901234567890"u8.ToArray();

        // When
        var result = Base32Encoder.Decode(Base32Encoder.Encode(data));

        // Then
        result.ShouldBe(data);
    }
}
