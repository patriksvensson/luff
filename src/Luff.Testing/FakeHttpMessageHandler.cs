using System.Net;
using System.Text;

namespace Luff.Agent.Tests.Fakes;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpStatusCode> _responder;
    private readonly Func<HttpRequestMessage, string?>? _body;

    public FakeHttpMessageHandler(
        Func<HttpRequestMessage, HttpStatusCode> responder, Func<HttpRequestMessage, string?>? body = null)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        _body = body;
    }

    public List<(HttpMethod Method, string Path)> Requests { get; } = new();
    public List<string?> Bodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty));
        Bodies.Add(requestBody);

        var response = new HttpResponseMessage(_responder(request));
        var body = _body?.Invoke(request);
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return response;
    }
}
