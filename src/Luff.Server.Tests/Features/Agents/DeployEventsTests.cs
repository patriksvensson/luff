using Luff.Protobuf;
using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Fleet;

public sealed class DeployEventsTests
{
    [Fact]
    public async Task Should_Deliver_Live_Events_In_Order()
    {
        // Given
        var hub = new DeployEvents();
        var id = Guid.NewGuid();
        var received = new List<DeployEvent>();
        var done = new TaskCompletionSource();
        using var subscription = hub.Subscribe(id, deployEvent =>
        {
            received.Add(deployEvent);
            if (deployEvent.Kind == DeployEventKind.Result)
            {
                done.SetResult();
            }

            return Task.CompletedTask;
        });

        // When
        hub.PublishProgress(id, "agent-1", DeployPhase.Pulling);
        hub.PublishProgress(id, "agent-1", DeployPhase.Starting);
        hub.PublishResult(id, "agent-1", healthy: true, runningTag: "v2", failureReason: null);
        await done.Task.WaitAsync(TestTimeout.Backstop);

        // Then
        received.Select(deployEvent => deployEvent.Phase)
            .ShouldBe([DeployPhase.Pulling, DeployPhase.Starting, null]);
        received[^1].ShouldSatisfyAllConditions(
            last => last.Kind.ShouldBe(DeployEventKind.Result),
            last => last.Healthy.ShouldBeTrue(),
            last => last.RunningTag.ShouldBe("v2"));
    }

    [Fact]
    public async Task Should_Replay_History_To_A_Late_Subscriber()
    {
        // Given
        var hub = new DeployEvents();
        var id = Guid.NewGuid();
        hub.PublishProgress(id, "agent-1", DeployPhase.Pulling);
        hub.PublishProgress(id, "agent-1", DeployPhase.Starting);

        // When
        var received = new List<DeployEvent>();
        var got = new TaskCompletionSource();
        using var subscription = hub.Subscribe(id, deployEvent =>
        {
            received.Add(deployEvent);
            if (received.Count == 2)
            {
                got.SetResult();
            }

            return Task.CompletedTask;
        });
        await got.Task.WaitAsync(TestTimeout.Backstop);

        // Then
        received.Select(deployEvent => deployEvent.Phase).ShouldBe([DeployPhase.Pulling, DeployPhase.Starting]);
    }

    [Fact]
    public async Task Should_Fan_Out_To_Multiple_Subscribers()
    {
        // Given
        var hub = new DeployEvents();
        var id = Guid.NewGuid();
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        using var one = hub.Subscribe(id, deployEvent =>
        {
            if (deployEvent.Kind == DeployEventKind.Result)
            {
                first.SetResult();
            }

            return Task.CompletedTask;
        });
        using var two = hub.Subscribe(id, deployEvent =>
        {
            if (deployEvent.Kind == DeployEventKind.Result)
            {
                second.SetResult();
            }

            return Task.CompletedTask;
        });

        // When
        hub.PublishResult(id, "agent-1", healthy: true, runningTag: "v2", failureReason: null);

        // Then
        await Task.WhenAll(first.Task, second.Task).WaitAsync(TestTimeout.Backstop);
    }
}
