using BuurmansRgb.Core;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts.Tests;

public sealed class EngineRpcTests : IDisposable
{
    private readonly string _pipeName = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly FakeEngineApi _api = new();
    private JsonRpc? _server;
    private EngineConnection? _client;

    public void Dispose()
    {
        _client?.Dispose();
        _server?.Dispose();
    }

    [Fact]
    public void PipeName_IsPerWindowsSession()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        Assert.Equal($"BuurmansRgb.Engine.{process.SessionId}", EngineRpc.PipeName);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsDevicesWithTheirZonesFromServer()
    {
        var client = await ConnectAsync();

        var devices = await client.Api.GetDevicesAsync(Ct);

        Assert.Equal(_api.Devices, devices);
        Assert.NotNull(devices[^1].Zones[0].Matrix);
    }

    [Fact]
    public async Task SetColorAsync_SendsDeviceAndColor()
    {
        var client = await ConnectAsync();

        await client.Api.SetColorAsync("fan", new RgbColor(255, 136, 0), Ct);
        await client.Api.SetColorAsync(null, RgbColor.White, Ct);

        Assert.Equal([("fan", new RgbColor(255, 136, 0)), (null, RgbColor.White)], _api.ColorCalls);
    }

    [Fact]
    public async Task GetLightingAsync_RoundTripsEveryField()
    {
        var client = await ConnectAsync();

        var lighting = await client.Api.GetLightingAsync("fan", Ct);

        Assert.Equal(_api.Lighting, lighting);
    }

    [Fact]
    public async Task Profiles_NamesAndCallsCrossThePipe()
    {
        var client = await ConnectAsync();

        var names = await client.Api.GetProfilesAsync(Ct);
        await client.Api.SaveProfileAsync("Werk", Ct);
        await client.Api.LoadProfileAsync("Gamen", Ct);
        await client.Api.DeleteProfileAsync("Nacht", Ct);

        Assert.Equal(["Gamen", "Nacht"], names);
        Assert.Equal(["save Werk", "load Gamen", "delete Nacht"], _api.ProfileCalls);
    }

    [Fact]
    public async Task FrameRendered_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<LightingFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.FrameRendered += (_, frame) => received.TrySetResult(frame);

        _api.RaiseFrame(new LightingFrame(7, [DeviceFrame.FromColors("fan", [new RgbColor(1, 2, 3)])]));

        var frame = await received.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal(7, frame.Sequence);
        Assert.Equal("fan", Assert.Single(frame.Devices).DeviceId);
        Assert.Equal([new RgbColor(1, 2, 3)], frame.Devices[0].ToColors());
    }

    [Fact]
    public async Task DevicesChanged_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.DevicesChanged += (_, _) => received.TrySetResult();

        _api.RaiseDevicesChanged();

        await received.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task SettingsSaved_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<SettingsSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.SettingsSaved += (_, result) => received.TrySetResult(result);

        _api.RaiseSaved(succeeded: false);

        Assert.False((await received.Task.WaitAsync(TestTimeouts.Short, Ct)).Succeeded);
    }

    [Fact]
    public async Task SetColorLoopAsync_ReachesServerWithColorsInOrder()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 255, 0), new(0, 255, 0), new(0, 0, 255)];

        await client.Api.SetColorLoopAsync("fan", colors, spread: false, Ct);

        var call = Assert.Single(_api.ColorLoopCalls);
        Assert.Equal("fan", call.DeviceId);
        Assert.Equal(colors, call.Colors);
        Assert.False(call.Spread);
    }

    [Fact]
    public async Task SetSpeedAsync_ReachesServer()
    {
        var client = await ConnectAsync();

        await client.Api.SetSpeedAsync(null, 2.5, Ct);

        Assert.Equal([(null, 2.5)], _api.SpeedCalls);
    }

    [Fact]
    public async Task LedColors_RoundTripThroughThePipe()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 0, 0), new(0, 255, 0)];

        await client.Api.SetLedColorsAsync("fan", 3, colors, Ct);
        var stored = await client.Api.GetLedColorsAsync("fan", Ct);

        var call = Assert.Single(_api.LedColorCalls);
        Assert.Equal(("fan", 3), (call.DeviceId, call.FirstLed));
        Assert.Equal(colors, call.Colors);
        Assert.Equal(_api.LedColors, stored);
    }

    [Fact]
    public async Task ResizeZoneAsync_SendsDeviceZoneAndLength()
    {
        var client = await ConnectAsync();

        await client.Api.ResizeZoneAsync("board", 1, 24, Ct);

        Assert.Equal([("board", 1, 24)], _api.ResizeCalls);
    }

    [Fact]
    public async Task ServerError_ReachesClientAsRemoteInvocationException()
    {
        var client = await ConnectAsync();

        var exception = await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetEffectAsync(null, "unknown", Ct));
        Assert.True(EngineRpc.IsRejectedByEngine(exception));
    }

    [Fact]
    public void IsRejectedByEngine_LostConnection_IsFalse()
    {
        Assert.False(EngineRpc.IsRejectedByEngine(new ConnectionLostException()));
    }

    [Fact]
    public async Task Disconnected_ServerStops_IsRaised()
    {
        var client = await ConnectAsync();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += (_, _) => disconnected.TrySetResult();

        _server!.Dispose();

        await disconnected.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task ConnectAsync_NoServer_ThrowsTimeout()
    {
        await Assert.ThrowsAsync<TimeoutException>(
            () => EngineConnection.ConnectAsync(_pipeName, TimeSpan.FromMilliseconds(100), Ct));
    }

    private async Task<EngineConnection> ConnectAsync()
    {
        var pipe = EngineRpc.CreateServerStream(_pipeName);
        var accepted = pipe.WaitForConnectionAsync(Ct);
        _client = await EngineConnection.ConnectAsync(_pipeName, TestTimeouts.Short, Ct);
        await accepted;
        _server = EngineRpc.StartServer(pipe, _api);
        return _client;
    }

    private sealed class FakeEngineApi : IEngineApi
    {
        public event EventHandler<LightingFrame>? FrameRendered;

        public event EventHandler? DevicesChanged;

        public event EventHandler<SettingsSaveResult>? SettingsSaved;

        public IReadOnlyList<DeviceInfo> Devices { get; } =
        [
            new("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]),
            new("board", "Motherboard", DeviceType.Motherboard, 4, [RgbZone.Fixed("Mainboard", 4), new RgbZone("ARGB header", 0, 0, 120)]),
            new("keyboard", "Keyboard", DeviceType.Keyboard, 5,
                [RgbZone.Fixed("Keys", 5) with { Matrix = new LedMatrix(2, 3, [0, 1, 2, 3, LedMatrix.NoLed, 4]) }]),
        ];

        public LightingInfo Lighting { get; } = new(new RgbColor(10, 20, 30), 0.25, EffectIds.Static, 1.5, new ColorLoop([new RgbColor(1, 2, 3), new RgbColor(4, 5, 6)], Spread: false));

        public List<(string? DeviceId, RgbColor Color)> ColorCalls { get; } = [];

        public IReadOnlyList<RgbColor> LedColors { get; } = [new(1, 1, 1), new(2, 2, 2)];

        public List<(string DeviceId, int FirstLed, IReadOnlyList<RgbColor> Colors)> LedColorCalls { get; } = [];

        public List<(string DeviceId, int ZoneIndex, int LedCount)> ResizeCalls { get; } = [];

        public void RaiseFrame(LightingFrame frame) => FrameRendered?.Invoke(this, frame);

        public List<(string? DeviceId, double Speed)> SpeedCalls { get; } = [];

        public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

        public void RaiseSaved(bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

        public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) => Task.FromResult(Devices);

        public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([EffectIds.Static]);

        public Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default) => Task.FromResult(Lighting);

        public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(LightingFrame.Empty);

        public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default)
        {
            ColorCalls.Add((deviceId, color));
            return Task.CompletedTask;
        }

        public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default) => Task.CompletedTask;

        public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
            throw new ArgumentException($"Unknown effect '{effectId}'.", nameof(effectId));

        public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default)
        {
            SpeedCalls.Add((deviceId, speed));
            return Task.CompletedTask;
        }

        public List<(string? DeviceId, IReadOnlyList<RgbColor> Colors, bool Spread)> ColorLoopCalls { get; } = [];

        public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default)
        {
            ColorLoopCalls.Add((deviceId, colors, spread));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default) => Task.FromResult(LedColors);

        public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
        {
            LedColorCalls.Add((deviceId, firstLed, colors));
            return Task.CompletedTask;
        }

        public Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default)
        {
            ResizeCalls.Add((deviceId, zoneIndex, ledCount));
            return Task.CompletedTask;
        }

        public List<string> ProfileCalls { get; } = [];

        public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["Gamen", "Nacht"]);

        public Task SaveProfileAsync(string name, CancellationToken ct = default) => RecordProfile($"save {name}");

        public Task LoadProfileAsync(string name, CancellationToken ct = default) => RecordProfile($"load {name}");

        public Task DeleteProfileAsync(string name, CancellationToken ct = default) => RecordProfile($"delete {name}");

        private Task RecordProfile(string call)
        {
            ProfileCalls.Add(call);
            return Task.CompletedTask;
        }
    }
}
