using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using StreamJsonRpc;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineIpcServerTests : IAsyncDisposable
{
    private const string Fan = "simulated-fan";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly string _pipeName = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly LightingController _lighting = new([new SimulatedDeviceProvider()]);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<EngineConnection> _clients = [];
    private readonly SemaphoreSlim _allClientsGone = new(0);
    private Task? _server;

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }

        await _shutdown.CancelAsync();
        if (_server is not null)
        {
            await _server;
        }

        _shutdown.Dispose();
        _allClientsGone.Dispose();
    }

    [Fact]
    public async Task AllClientsGone_RunsOnlyAfterTheLastClientLeaves()
    {
        var first = await StartAndConnectAsync();
        var second = await ConnectAsync();
        await second.Api.GetDevicesAsync(TestContext.Current.CancellationToken);

        await DisconnectAsync(first);
        Assert.False(await _allClientsGone.WaitAsync(200, TestContext.Current.CancellationToken));

        await DisconnectAsync(second);
        Assert.True(await _allClientsGone.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetDevicesAsync_ListsSimulatedDevices()
    {
        var client = await StartAndConnectAsync();

        var devices = await client.Api.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [("simulated-strip", 30), (Fan, 12), ("simulated-keyboard", 104)],
            devices.Select(device => (device.Id, device.LedCount)));
    }

    [Fact]
    public async Task SetColorAsync_FrameReachesClientWithin100Milliseconds()
    {
        var client = await StartAndConnectAsync();
        var frames = new FrameCollector(client.Api);
        await client.Api.SetColorAsync(null, RgbColor.White, TestContext.Current.CancellationToken);
        await frames.WaitForAsync(RgbColor.White);

        var stopwatch = Stopwatch.StartNew();
        await client.Api.SetColorAsync(null, Orange, TestContext.Current.CancellationToken);
        await frames.WaitForAsync(Orange);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"The frame took {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task SetColorAsync_ForOneDevice_ChangesOnlyThatDevice()
    {
        var client = await StartAndConnectAsync();

        await client.Api.SetColorAsync(Fan, Orange, TestContext.Current.CancellationToken);

        Assert.Equal(Orange, (await client.Api.GetLightingAsync(Fan, TestContext.Current.CancellationToken)).Color);
        Assert.Equal(RgbColor.White, (await client.Api.GetLightingAsync(null, TestContext.Current.CancellationToken)).Color);
        var frame = await client.Api.GetFrameAsync(TestContext.Current.CancellationToken);
        Assert.All(frame.Devices.Single(device => device.DeviceId == Fan).ToColors(), led => Assert.Equal(Orange, led));
    }

    [Fact]
    public async Task SetBrightnessAsync_ScalesTheRenderedColor()
    {
        var client = await StartAndConnectAsync();

        await client.Api.SetBrightnessAsync(null, 0.5, TestContext.Current.CancellationToken);

        var frame = await client.Api.GetFrameAsync(TestContext.Current.CancellationToken);
        Assert.All(frame.Devices.SelectMany(device => device.ToColors()), led => Assert.Equal(RgbColor.White.Scale(0.5), led));
        Assert.Equal(0.5, (await client.Api.GetLightingAsync(null, TestContext.Current.CancellationToken)).Brightness);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task SetBrightnessAsync_OutOfRange_IsRejected(double brightness)
    {
        var client = await StartAndConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetBrightnessAsync(null, brightness, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetEffectAsync_KnownEffect_IsStored()
    {
        var client = await StartAndConnectAsync();

        await client.Api.SetEffectAsync(Fan, EffectIds.Static, TestContext.Current.CancellationToken);

        Assert.Equal([EffectIds.Static], await client.Api.GetEffectsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EffectIds.Static, (await client.Api.GetLightingAsync(Fan, TestContext.Current.CancellationToken)).EffectId);
    }

    [Fact]
    public async Task SetEffectAsync_UnknownEffect_IsRejected()
    {
        var client = await StartAndConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetEffectAsync(null, "disco", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetColorAsync_UnknownDevice_IsRejected()
    {
        var client = await StartAndConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetColorAsync("missing", Orange, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TwoClients_BothReceiveFrames()
    {
        var first = await StartAndConnectAsync();
        var second = await ConnectAsync();
        var firstFrames = new FrameCollector(first.Api);
        var secondFrames = new FrameCollector(second.Api);

        await first.Api.SetColorAsync(null, Orange, TestContext.Current.CancellationToken);

        await firstFrames.WaitForAsync(Orange);
        await secondFrames.WaitForAsync(Orange);
    }

    [Fact]
    public async Task ClientDisconnects_ServerKeepsAcceptingAndStopsSendingToIt()
    {
        var first = await StartAndConnectAsync();
        await first.Api.GetDevicesAsync(TestContext.Current.CancellationToken);
        await DisconnectAsync(first);

        var second = await ConnectAsync();
        var frames = new FrameCollector(second.Api);
        await second.Api.SetColorAsync(null, Orange, TestContext.Current.CancellationToken);

        await frames.WaitForAsync(Orange);
    }

    [Fact]
    public async Task Cancelled_StopsListening()
    {
        await StartAndConnectAsync();

        await _shutdown.CancelAsync();
        await _server!.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<TimeoutException>(
            () => EngineConnection.ConnectAsync(_pipeName, TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken));
    }

    private async Task<EngineConnection> StartAndConnectAsync()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        _lighting.Apply(LightingState.Uniform(DeviceLighting.Default));
        _server = new EngineIpcServer(_pipeName, _lighting, () => _allClientsGone.Release()).RunAsync(_shutdown.Token);
        return await ConnectAsync();
    }

    private async Task DisconnectAsync(EngineConnection client)
    {
        _clients.Remove(client);
        await client.DisposeAsync();
    }

    private async Task<EngineConnection> ConnectAsync()
    {
        var client = await EngineConnection.ConnectAsync(_pipeName, Timeout, TestContext.Current.CancellationToken);
        _clients.Add(client);
        return client;
    }

    // Remembers the latest frame: the frame for a change can arrive before the call returns.
    private sealed class FrameCollector
    {
        private readonly Lock _gate = new();
        private readonly List<(RgbColor Color, TaskCompletionSource Seen)> _waiters = [];
        private LightingFrame? _latest;

        public FrameCollector(IEngineApi api) => api.FrameRendered += OnFrame;

        public Task WaitForAsync(RgbColor color)
        {
            var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                if (_latest is not null && Shows(_latest, color))
                {
                    return Task.CompletedTask;
                }

                _waiters.Add((color, seen));
            }

            return seen.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }

        private static bool Shows(LightingFrame frame, RgbColor color) =>
            frame.Devices.All(device => device.ToColors().All(led => led == color));

        private void OnFrame(object? sender, LightingFrame frame)
        {
            lock (_gate)
            {
                _latest = frame;
                foreach (var (color, seen) in _waiters)
                {
                    if (Shows(frame, color))
                    {
                        seen.TrySetResult();
                    }
                }
            }
        }
    }
}
