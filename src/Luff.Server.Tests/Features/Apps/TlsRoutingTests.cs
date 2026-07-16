using Luff.Protobuf;
using Luff.Server.Features;
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
}
