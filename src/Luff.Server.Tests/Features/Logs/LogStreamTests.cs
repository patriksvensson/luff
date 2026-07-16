using Luff.Protobuf;
using Luff.Server.Features;
using Luff.Server.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Logs;

public sealed class LogStreamTests
{
    [Fact]
    public async Task Should_Start_The_Agent_Stream_And_Yield_Published_Chunks()
    {
        // Given
        var connections = new FakeAgentConnections();
        connections.Register("agent-1");
        var stream = new LogStream(connections);
        await using var enumerator = stream.Tail("agent-1", "web", 200).GetAsyncEnumerator();

        // When
        var next = enumerator.MoveNextAsync();
        var start = await connections.GetChannel("agent-1").ReadAsync();
        var streamId = Guid.Parse(start.StartLogStream.StreamId);
        stream.PublishChunk(streamId, new LogEvent(null, LogStreamKind.Stdout, "hello", "agent-1"));

        // Then
        start.StartLogStream.ShouldSatisfyAllConditions(
            request => request.App.ShouldBe("web"),
            request => request.Tail.ShouldBe(200));
        (await next).ShouldBeTrue();
        enumerator.Current.Line.ShouldBe("hello");
    }

    [Fact]
    public async Task Should_Send_StopLogStream_When_The_Consumer_Stops()
    {
        // Given
        var connections = new FakeAgentConnections();
        connections.Register("agent-1");
        var stream = new LogStream(connections);
        var enumerator = stream.Tail("agent-1", "web", 200).GetAsyncEnumerator();
        var next = enumerator.MoveNextAsync();
        var start = await connections.GetChannel("agent-1").ReadAsync();
        var streamId = Guid.Parse(start.StartLogStream.StreamId);

        // When
        stream.PublishChunk(streamId, new LogEvent(null, LogStreamKind.Stdout, "hi", "agent-1"));
        (await next).ShouldBeTrue();
        await enumerator.DisposeAsync();

        // Then
        var stop = await connections.GetChannel("agent-1").ReadAsync();
        stop.PayloadCase.ShouldBe(ControlMessage.PayloadOneofCase.StopLogStream);
        stop.StopLogStream.StreamId.ShouldBe(start.StartLogStream.StreamId);
    }

    [Fact]
    public void Should_Drop_Chunks_For_Unknown_Streams()
    {
        // Given
        var stream = new LogStream(new FakeAgentConnections());

        // When
        var exception = Record.Exception(() =>
            stream.PublishChunk(Guid.NewGuid(), new LogEvent(null, LogStreamKind.Stdout, "orphan", "agent-1")));

        // Then
        exception.ShouldBeNull();
    }
}
