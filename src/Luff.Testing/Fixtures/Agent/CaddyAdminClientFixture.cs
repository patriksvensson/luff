using System.Net;
using Luff.Agent.Tests.Fakes;

namespace Luff.Agent.Tests.Fixtures;

public sealed class CaddyAdminClientFixture
{
    public FakeHttpMessageHandler Handler { get; }
    public CaddyClient Client { get; }

    public CaddyAdminClientFixture(
        Func<HttpRequestMessage, HttpStatusCode> responder, Func<HttpRequestMessage, string?>? body = null)
    {
        Handler = new FakeHttpMessageHandler(responder, body);
        Client = new CaddyClient(
            new HttpClient(Handler)
            {
                BaseAddress = new Uri("http://localhost:2019")
            });
    }
}
