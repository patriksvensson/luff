using Grpc.Core;

namespace Luff.Server.Tests.Fakes;

public sealed class FakeServerCallContext : ServerCallContext
{
    private readonly CancellationToken _cancellationToken;
    private readonly Metadata _requestHeaders = [];
    private readonly Metadata _responseTrailers = [];
    private readonly AuthContext _authContext = new(string.Empty, new());

    protected override string MethodCore => "luff.link.v1.Link/Connect";

    protected override string HostCore => "localhost";

    protected override string PeerCore => "test-peer";

    protected override DateTime DeadlineCore => DateTime.MaxValue;

    protected override Metadata RequestHeadersCore => _requestHeaders;

    protected override CancellationToken CancellationTokenCore => _cancellationToken;

    protected override Metadata ResponseTrailersCore => _responseTrailers;

    protected override Status StatusCore { get; set; }

    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore => _authContext;

    public FakeServerCallContext(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
    {
        throw new NotSupportedException();
    }

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        return Task.CompletedTask;
    }
}