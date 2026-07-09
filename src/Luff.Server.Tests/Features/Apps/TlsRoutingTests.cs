using Luff.Protobuf;
using Luff.Server.Features;
using Luff.Server.Infrastructure;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Apps;

public sealed class TlsRoutingTests
{
    [Theory]
    [InlineData(TlsMode.Managed, "web.example.com", TlsRoute.Managed)]
    [InlineData(TlsMode.Managed, "web.127.0.0.1.sslip.io", TlsRoute.Http)]
    [InlineData(TlsMode.External, "web.example.com", TlsRoute.External)]
    [InlineData(TlsMode.External, "web.127.0.0.1.sslip.io", TlsRoute.Http)]
    public void Should_Resolve_The_Route(TlsMode mode, string domain, TlsRoute expected)
    {
        // Given
        var app = new App { Name = "web", Image = "nginx", Domain = domain, InternalPort = 80, TlsMode = mode };

        // When
        var route = TlsRouting.Resolve(app);

        // Then
        route.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Should_Default_To_Managed_When_The_Mode_Is_Missing(string? value)
    {
        TlsRouting.ParseMode(value).ShouldBe(TlsMode.Managed);
    }

    [Theory]
    [InlineData("managed", TlsMode.Managed)]
    [InlineData("External", TlsMode.External)]
    [InlineData("EXTERNAL", TlsMode.External)]
    public void Should_Parse_A_Known_Mode(string value, TlsMode expected)
    {
        TlsRouting.ParseMode(value).ShouldBe(expected);
    }

    [Fact]
    public void Should_Throw_On_An_Unknown_Mode()
    {
        var exception = Record.Exception(() => TlsRouting.ParseMode("bogus"));

        exception.ShouldBeOfType<InvalidTlsModeException>();
    }
}
