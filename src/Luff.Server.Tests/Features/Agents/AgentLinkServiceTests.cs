using Grpc.Core;
using Luff.Protobuf;
using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Fleet;

public sealed class AgentLinkServiceTests
{
    [Fact]
    public async Task Should_Write_Welcome_When_Receiving_Hello()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        var written = await fixture.Connect(
            AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret, "2.0.0"));

        // Then
        written.ShouldHaveSingleItem()
            .PayloadCase.ShouldBe(ControlMessage.PayloadOneofCase.Welcome);
    }

    [Fact]
    public async Task Should_Register_Agent_After_Connect()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        var written = await fixture.Connect(
            AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret, "2.0.0"));

        // Then
        fixture.Registry.List()
            .ShouldContain(agent => agent.Name == "local" && agent.Version == "2.0.0");
    }

    [Fact]
    public async Task Should_Record_The_Front_Door_Host_On_Enroll()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        await fixture.Connect(
            AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret, "2.0.0", hostsFrontDoor: true));

        // Then
        fixture.Registry.IsFrontDoorHost("local").ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Reject_Agent_On_Exception()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        var result = await Record.ExceptionAsync(
            () => fixture.Connect(AgentLinkServiceFixture.Hello("local", "wrong-secret")));

        // Then
        fixture.Registry.List().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Not_Register_Agent_On_Invalid_Secret()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        var result = await Record.ExceptionAsync(
            () => fixture.Connect(AgentLinkServiceFixture.Hello("local", "wrong-secret")));

        // Then
        result.ShouldNotBeNull();
        fixture.Registry.List().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Expect_Hello_From_Agent_On_First_Frame()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();
        var pong = new AgentMessage
        {
            Pong = new Pong
            {
                Sequence = 1,
            },
        };

        // When
        var exception = await Record.ExceptionAsync(() => fixture.Connect(pong));

        // Then
        exception.ShouldBeOfType<RpcException>().StatusCode.ShouldBe(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task Closing_The_Stream_Before_Enrolling_Is_Rejected()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        var exception = await Record.ExceptionAsync(() => fixture.Connect());

        // Then
        exception.ShouldBeOfType<RpcException>()
            .StatusCode.ShouldBe(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task Should_Dispatch_Agent_Connected_After_Enroll()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        await fixture.Connect(AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret));

        // Then
        fixture.Sender.Received<AgentConnectedHandler.Request>()
            .ShouldContain(request => request.AgentName == "local");
    }

    [Fact]
    public async Task Should_Dispatch_Agent_Disconnected_When_The_Agent_Disconnects()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();

        // When
        await fixture.Connect(AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret));

        // Then
        fixture.Sender.Received<AgentDisconnectedHandler.Request>()
            .ShouldContain(request => request.AgentName == "local");
    }

    [Fact]
    public async Task Should_Bridge_A_LogChunk_To_The_Log_Stream()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();
        fixture.Connections.Register("local");
        await using var logs = fixture.Logs.Tail("local", "web", 200).GetAsyncEnumerator();
        var next = logs.MoveNextAsync();
        var start = await fixture.Connections.GetChannel("local").ReadAsync();

        // When
        await fixture.Connect(
            AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret),
            new AgentMessage
            {
                LogChunk = new LogChunk
                {
                    StreamId = start.StartLogStream.StreamId,
                    Stream = LogStreamKind.Stderr,
                    Line = "boom",
                    Timestamp = string.Empty,
                },
            });

        // Then
        (await next).ShouldBeTrue();
        logs.Current.ShouldSatisfyAllConditions(
            chunk => chunk.Line.ShouldBe("boom"),
            chunk => chunk.Stream.ShouldBe(LogStreamKind.Stderr),
            chunk => chunk.Agent.ShouldBe("local"));
    }

    [Fact]
    public async Task Should_Return_From_Connect_When_The_Application_Is_Stopping()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();
        var (running, reader) = fixture.ConnectOpenEnded(
            AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret));
        await reader.Parked;

        // When
        fixture.Lifetime.StopApplication();

        // Then
        await running.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Not_Dispatch_Agent_Disconnected_When_The_Application_Is_Stopping()
    {
        // Given
        var fixture = new AgentLinkServiceFixture();
        var (running, reader) = fixture.ConnectOpenEnded(
            AgentLinkServiceFixture.Hello("local", AgentLinkServiceFixture.ValidSecret));
        await reader.Parked;

        // When
        fixture.Lifetime.StopApplication();
        await running.WaitAsync(TimeSpan.FromSeconds(5));

        // Then
        fixture.Sender.Received<AgentDisconnectedHandler.Request>().ShouldBeEmpty();
    }
}