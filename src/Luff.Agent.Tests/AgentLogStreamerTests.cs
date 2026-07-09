using Luff.Agent.Tests.Fixtures;
using Luff.Protobuf;
using Shouldly;
using Xunit;

namespace Luff.Agent.Tests;

public sealed class AgentLogStreamerTests
{
    [Fact]
    public async Task Should_Emit_A_LogChunk_Per_Docker_Line()
    {
        // Given
        var fixture = new AgentLogStreamerFixture();
        fixture.DockerCompose.LogLines =
        [
            new DockerLogLine(
                new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero), DockerLogStreamKind.Stdout, "hello"),
            new DockerLogLine(null, DockerLogStreamKind.Stderr, "oops"),
        ];

        // When
        await fixture.StreamAsync("s1", "web", 200);

        // Then
        fixture.Emitted.Count.ShouldBe(2);
        fixture.Emitted[0].LogChunk.ShouldSatisfyAllConditions(
            chunk => chunk.StreamId.ShouldBe("s1"),
            chunk => chunk.Stream.ShouldBe(LogStreamKind.Stdout),
            chunk => chunk.Line.ShouldBe("hello"),
            chunk => chunk.Timestamp.ShouldBe("2026-07-04T10:00:00.0000000+00:00"));
    }

    [Fact]
    public async Task Should_Mark_Stderr_Lines_And_Omit_An_Unknown_Timestamp()
    {
        // Given
        var fixture = new AgentLogStreamerFixture();
        fixture.DockerCompose.LogLines =
        [
            new DockerLogLine(null, DockerLogStreamKind.Stderr, "oops"),
        ];

        // When
        await fixture.StreamAsync("s1", "web", 200);

        // Then
        fixture.Emitted[0].LogChunk.ShouldSatisfyAllConditions(
            chunk => chunk.Stream.ShouldBe(LogStreamKind.Stderr),
            chunk => chunk.Timestamp.ShouldBe(string.Empty));
    }

    [Fact]
    public async Task Should_Pass_The_App_And_Tail_To_Docker()
    {
        // Given
        var fixture = new AgentLogStreamerFixture();

        // When
        await fixture.StreamAsync("s1", "web", 150);

        // Then
        fixture.DockerCompose.StreamedApp.ShouldBe("web");
        fixture.DockerCompose.StreamedTail.ShouldBe(150);
    }
}
