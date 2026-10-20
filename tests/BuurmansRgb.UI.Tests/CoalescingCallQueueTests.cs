using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

public sealed class CoalescingCallQueueTests
{
    private readonly List<string> _calls = [];
    private readonly List<Exception> _errors = [];

    [Fact]
    public void Post_WhileIdle_RunsAtOnce()
    {
        var queue = CreateQueue();

        queue.Post("a", () => Record("a1"));

        Assert.Equal(["a1"], _calls);
    }

    [Fact]
    public async Task Post_WhileBusy_KeepsOnlyLatestOfSameKey()
    {
        var queue = CreateQueue();
        var gate = new TaskCompletionSource();
        queue.Post("color", () => RecordAfter(gate.Task, "color1"));

        queue.Post("color", () => Record("color2"));
        queue.Post("color", () => Record("color3"));
        gate.SetResult();

        await WaitForCallsAsync(2);
        Assert.Equal(["color1", "color3"], _calls);
    }

    [Fact]
    public async Task Post_WhileBusy_KeepsOrderAcrossKeys()
    {
        var queue = CreateQueue();
        var gate = new TaskCompletionSource();
        queue.Post("color", () => RecordAfter(gate.Task, "color1"));

        queue.Post("color", () => Record("color2"));
        queue.Post("brightness", () => Record("brightness1"));
        queue.Post("color", () => Record("color3"));
        gate.SetResult();

        await WaitForCallsAsync(4);
        Assert.Equal(["color1", "color2", "brightness1", "color3"], _calls);
    }

    [Fact]
    public void FailingCall_IsReportedAndQueueContinues()
    {
        var queue = CreateQueue();
        var failure = new InvalidOperationException("Boom.");

        queue.Post("a", () => Task.FromException(failure));
        queue.Post("b", () => Record("b1"));

        Assert.Equal([failure], _errors);
        Assert.Equal(["b1"], _calls);
    }

    private CoalescingCallQueue CreateQueue() => new(exception =>
    {
        lock (_errors)
        {
            _errors.Add(exception);
        }
    });

    private Task Record(string call)
    {
        lock (_calls)
        {
            _calls.Add(call);
        }

        return Task.CompletedTask;
    }

    private async Task RecordAfter(Task gate, string call)
    {
        await gate;
        await Record(call);
    }

    private async Task WaitForCallsAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            lock (_calls)
            {
                if (_calls.Count >= count)
                {
                    return;
                }
            }

            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the calls.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
