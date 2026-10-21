using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Providers.Simulated;
using StreamJsonRpc;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineIpcServerTests : IAsyncDisposable
{
    private const string Fan = "simulated-fan";

    private readonly string _pipeName = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly LightingController _lighting;
    private readonly SimulatedDevicesSwitch _simulatedDevices;
    private readonly FakeServerStatus _openRgb = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<EngineConnection> _clients = [];
    private readonly SemaphoreSlim _allClientsGone = new(0);
    private Task? _server;

    public EngineIpcServerTests()
    {
        var simulated = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
        _lighting = new LightingController([simulated]);
        _simulatedDevices = new SimulatedDevicesSwitch(simulated, _lighting);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await _shutdown.CancelAsync();
        if (_server is not null)
        {
            await _server;
        }

        _shutdown.Dispose();
        _allClientsGone.Dispose();
        _simulatedDevices.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public async Task AllClientsGone_RunsOnlyAfterTheLastClientLeaves()
    {
        var first = await StartAndConnectAsync();
        var second = await ConnectAsync();
        await second.Api.GetDevicesAsync(Ct);

        await DisconnectAsync(first);
        Assert.False(await _allClientsGone.WaitAsync(200, Ct));

        await DisconnectAsync(second);
        Assert.True(await _allClientsGone.WaitAsync(TestTimeouts.Short, Ct));
    }

    [Fact]
    public async Task GetDevicesAsync_ListsSimulatedDevices()
    {
        var client = await StartAndConnectAsync();

        var devices = await client.Api.GetDevicesAsync(Ct);

        Assert.Equal(
            [("simulated-strip", 30), (Fan, 12), ("simulated-keyboard", 104), ("simulated-motherboard", 7), ("simulated-ram-1", 8), ("simulated-ram-2", 8), ("simulated-gpu", 13),
                ("simulated-fan-dual", 20), ("simulated-argb", 20), ("simulated-case", 10), ("simulated-mouse", 12), ("simulated-headset", 2)],
            devices.Select(device => (device.Id, device.LedCount)));
        Assert.NotNull(devices.Single(device => device.Id == "simulated-keyboard").Zones[0].Matrix);
        Assert.All(devices, device => Assert.Equal(DeviceSource.Simulated, device.Source));
    }

    [Fact]
    public async Task SetColorAsync_FrameReachesClientWithin100Milliseconds()
    {
        var client = await StartAndConnectAsync();
        var frames = new FrameCollector(client.Api);
        await client.Api.SetColorAsync(null, RgbColor.White, Ct);
        await frames.WaitForAsync(RgbColor.White);

        // The fastest of a few round trips, so one hiccup on a busy machine does not fail the
        // test while a slow pipe still does.
        List<long> timings = [];
        foreach (var color in new[] { Orange, RgbColor.White, Orange, RgbColor.White, Orange })
        {
            var stopwatch = Stopwatch.StartNew();
            await client.Api.SetColorAsync(null, color, Ct);
            await frames.WaitForAsync(color);
            timings.Add(stopwatch.ElapsedMilliseconds);
        }

        Assert.True(timings.Min() < 100, $"The frames took {string.Join(", ", timings)} ms.");
    }

    [Fact]
    public async Task SetColorAsync_ForOneDevice_ChangesOnlyThatDevice()
    {
        var client = await StartAndConnectAsync();

        await client.Api.SetColorAsync(Fan, Orange, Ct);

        Assert.Equal(Orange, (await client.Api.GetLightingAsync(Fan, Ct)).Color);
        Assert.Equal(RgbColor.White, (await client.Api.GetLightingAsync(null, Ct)).Color);
        var frame = await client.Api.GetFrameAsync(Ct);
        Assert.All(frame.Devices.Single(device => device.DeviceId == Fan).ToColors(), led => Assert.Equal(Orange, led));
    }

    [Fact]
    public async Task SetBrightnessAsync_ScalesTheRenderedColor()
    {
        var client = await StartAndConnectAsync();

        await client.Api.SetBrightnessAsync(null, 0.5, Ct);

        var frame = await client.Api.GetFrameAsync(Ct);
        Assert.All(frame.Devices.SelectMany(device => device.ToColors()), led => Assert.Equal(RgbColor.White.Scale(0.5), led));
        Assert.Equal(0.5, (await client.Api.GetLightingAsync(null, Ct)).Brightness);
    }

    // Only the mapping to the wire: the range itself is tested in EngineApiTests and
    // LightingControllerValidationTests.
    [Fact]
    public async Task ArgumentError_ReachesTheUiAsRemoteInvocationException()
    {
        var client = await StartAndConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetBrightnessAsync(null, 1.1, Ct));
    }

    [Fact]
    public async Task SetEffectAsync_KnownEffect_IsStored()
    {
        var client = await StartAndConnectAsync();

        await client.Api.SetEffectAsync(Fan, EffectIds.Rainbow, Ct);

        Assert.Equal(EffectCatalog.Ids, await client.Api.GetEffectsAsync(Ct));
        Assert.Equal(EffectIds.Rainbow, (await client.Api.GetLightingAsync(Fan, Ct)).EffectId);
    }

    [Fact]
    public async Task SetEffectAsync_UnknownEffect_IsRejected()
    {
        var client = await StartAndConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetEffectAsync(null, "disco", Ct));
    }

    [Fact]
    public async Task SetColorAsync_UnknownDevice_IsRejected()
    {
        var client = await StartAndConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetColorAsync("missing", Orange, Ct));
    }

    [Fact]
    public async Task FlashDeviceAsync_FlashesOnlyThatDeviceAndKeepsItsLighting()
    {
        var client = await StartAndConnectAsync();
        await client.Api.SetColorAsync(null, Orange, Ct);
        var flashed = new TaskCompletionSource<LightingFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.FrameRendered += (_, frame) =>
        {
            if (frame.Devices.Single(device => device.DeviceId == Fan).ToColors().All(led => led == RgbColor.White))
            {
                flashed.TrySetResult(frame);
            }
        };

        await client.Api.FlashDeviceAsync(Fan, Ct);

        var frame = await flashed.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.All(frame.Devices.Where(device => device.DeviceId != Fan).SelectMany(device => device.ToColors()), led => Assert.Equal(Orange, led));
        Assert.Equal(Orange, (await client.Api.GetLightingAsync(Fan, Ct)).Color);
    }

    // Flashing changes nothing, so a device that just went away is no error for the settings app.
    [Fact]
    public async Task FlashDeviceAsync_UnknownDevice_IsIgnored()
    {
        var client = await StartAndConnectAsync();
        var sequence = (await client.Api.GetFrameAsync(Ct)).Sequence;

        await client.Api.FlashDeviceAsync("missing", Ct);

        Assert.Equal(sequence, (await client.Api.GetFrameAsync(Ct)).Sequence);
    }

    [Fact]
    public async Task SetSimulatedDevicesAsync_HidesThemAndTheClientHearsOfIt()
    {
        var client = await StartAndConnectAsync();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.DevicesChanged += (_, _) => changed.TrySetResult();

        await client.Api.SetSimulatedDevicesAsync(SimulatedDevicesMode.Never, Ct);

        await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal(SimulatedDevicesMode.Never, await client.Api.GetSimulatedDevicesAsync(Ct));
        Assert.Empty(await client.Api.GetDevicesAsync(Ct));
    }

    [Fact]
    public async Task OpenRgbStatus_ReachesTheClient()
    {
        var client = await StartAndConnectAsync();
        var received = new TaskCompletionSource<ServerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.OpenRgbStatusChanged += (_, status) => received.TrySetResult(status);
        Assert.Equal(ServerStatus.Connecting, await client.Api.GetOpenRgbStatusAsync(Ct));

        _openRgb.Change(new ServerStatus(ServerState.Unreachable));

        Assert.Equal(new ServerStatus(ServerState.Unreachable), await received.Task.WaitAsync(TestTimeouts.Short, Ct));
    }

    [Fact]
    public async Task TwoClients_BothReceiveFrames()
    {
        var first = await StartAndConnectAsync();
        var second = await ConnectAsync();
        var firstFrames = new FrameCollector(first.Api);
        var secondFrames = new FrameCollector(second.Api);

        await first.Api.SetColorAsync(null, Orange, Ct);

        await firstFrames.WaitForAsync(Orange);
        await secondFrames.WaitForAsync(Orange);
    }

    [Fact]
    public async Task ClientDisconnects_ServerKeepsAcceptingAndStopsSendingToIt()
    {
        var first = await StartAndConnectAsync();
        await first.Api.GetDevicesAsync(Ct);
        await DisconnectAsync(first);

        var second = await ConnectAsync();
        var frames = new FrameCollector(second.Api);
        await second.Api.SetColorAsync(null, Orange, Ct);

        await frames.WaitForAsync(Orange);
    }

    [Fact]
    public async Task ProfileAutomation_IsSavedOverThePipeAndAnInvalidProgramIsRejected()
    {
        var client = await StartAndConnectAsync();
        await client.Api.SaveProfileAsync("Gamen", Ct);

        await client.Api.SetProfileAutomationAsync("Gamen", new ProfileAutomation(new TimeOnly(20, 30), "eldenring"), Ct);

        Assert.Equal(new ProfileAutomation(new TimeOnly(20, 30), "eldenring.exe"), await client.Api.GetProfileAutomationAsync("Gamen", Ct));
        var error = await Assert.ThrowsAnyAsync<Exception>(() => client.Api.SetProfileAutomationAsync("Gamen", new ProfileAutomation(Program: "game?.exe"), Ct));
        Assert.True(EngineRpc.IsRejectedByEngine(error));
    }

    [Fact]
    public async Task Cancelled_StopsListening()
    {
        await StartAndConnectAsync();

        await _shutdown.CancelAsync();
        await _server!.WaitAsync(TestTimeouts.Short, Ct);

        await Assert.ThrowsAsync<TimeoutException>(
            () => EngineConnection.ConnectAsync(_pipeName, TimeSpan.FromMilliseconds(200), Ct));
    }

    private async Task<EngineConnection> StartAndConnectAsync()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(LightingStates.Uniform(DeviceLighting.Default));
        _server = new EngineIpcServer(_pipeName, () => new EngineApi(_lighting, new ProfileLibrary(new InMemoryProfileStore(), _lighting), _simulatedDevices, _openRgb), () => _allClientsGone.Release()).RunAsync(_shutdown.Token);
        return await ConnectAsync();
    }

    private async Task DisconnectAsync(EngineConnection client)
    {
        _clients.Remove(client);
        client.Dispose();
    }

    private async Task<EngineConnection> ConnectAsync()
    {
        var client = await EngineConnection.ConnectAsync(_pipeName, TestTimeouts.Short, Ct);
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

            return seen.Task.WaitAsync(TestTimeouts.Short, Ct);
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
