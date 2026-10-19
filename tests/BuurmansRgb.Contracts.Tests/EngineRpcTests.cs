using BuurmansRgb.Core;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts.Tests;

public sealed class EngineRpcTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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

        var devices = await client.Api.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_api.Devices, devices);
    }

    [Fact]
    public async Task SetColorAsync_SendsDeviceAndColor()
    {
        var client = await ConnectAsync();

        await client.Api.SetColorAsync("fan", new RgbColor(255, 136, 0), TestContext.Current.CancellationToken);
        await client.Api.SetColorAsync(null, RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Equal([("fan", new RgbColor(255, 136, 0)), (null, RgbColor.White)], _api.ColorCalls);
    }

    [Fact]
    public async Task GetLightingAsync_RoundTripsColorBrightnessAndEffect()
    {
        var client = await ConnectAsync();

        var lighting = await client.Api.GetLightingAsync("fan", TestContext.Current.CancellationToken);

        Assert.Equal(_api.Lighting, lighting);
    }

    [Fact]
    public async Task FrameRendered_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<LightingFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.FrameRendered += (_, frame) => received.TrySetResult(frame);

        _api.RaiseFrame(new LightingFrame(7, [DeviceFrame.FromColors("fan", [new RgbColor(1, 2, 3)])]));

        var frame = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
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

        await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LedColors_RoundTripThroughThePipe()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 0, 0), new(0, 255, 0)];

        await client.Api.SetLedColorsAsync("fan", 3, colors, TestContext.Current.CancellationToken);
        var stored = await client.Api.GetLedColorsAsync("fan", TestContext.Current.CancellationToken);

        var call = Assert.Single(_api.LedColorCalls);
        Assert.Equal(("fan", 3), (call.DeviceId, call.FirstLed));
        Assert.Equal(colors, call.Colors);
        Assert.Equal(_api.LedColors, stored);
    }

    [Fact]
    public async Task ResizeZoneAsync_SendsDeviceZoneAndLength()
    {
        var client = await ConnectAsync();

        await client.Api.ResizeZoneAsync("board", 1, 24, TestContext.Current.CancellationToken);

        Assert.Equal([("board", 1, 24)], _api.ResizeCalls);
    }

    [Fact]
    public async Task ServerError_ReachesClientAsRemoteInvocationException()
    {
        var client = await ConnectAsync();

        await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetEffectAsync(null, "unknown", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disconnected_ServerStops_IsRaised()
    {
        var client = await ConnectAsync();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += (_, _) => disconnected.TrySetResult();

        _server!.Dispose();

        await disconnected.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConnectAsync_NoServer_ThrowsTimeout()
    {
        await Assert.ThrowsAsync<TimeoutException>(
            () => EngineConnection.ConnectAsync(_pipeName, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
    }

    private async Task<EngineConnection> ConnectAsync()
    {
        var pipe = EngineRpc.CreateServerStream(_pipeName);
        var accepted = pipe.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        _client = await EngineConnection.ConnectAsync(_pipeName, Timeout, TestContext.Current.CancellationToken);
        await accepted;
        _server = EngineRpc.StartServer(pipe, _api);
        return _client;
    }

    private sealed class FakeEngineApi : IEngineApi
    {
        public event EventHandler<LightingFrame>? FrameRendered;

        public event EventHandler? DevicesChanged;

        public IReadOnlyList<DeviceInfo> Devices { get; } =
        [
            new("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]),
            new("board", "Motherboard", DeviceType.Motherboard, 4, [RgbZone.Fixed("Mainboard", 4), new RgbZone("ARGB header", 0, 0, 120)]),
        ];

        public LightingInfo Lighting { get; } = new(new RgbColor(10, 20, 30), 0.25, EffectIds.Static);

        public List<(string? DeviceId, RgbColor Color)> ColorCalls { get; } = [];

        public IReadOnlyList<RgbColor> LedColors { get; } = [new(1, 1, 1), new(2, 2, 2)];

        public List<(string DeviceId, int FirstLed, IReadOnlyList<RgbColor> Colors)> LedColorCalls { get; } = [];

        public List<(string DeviceId, int ZoneIndex, int LedCount)> ResizeCalls { get; } = [];

        public void RaiseFrame(LightingFrame frame) => FrameRendered?.Invoke(this, frame);

        public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

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
    }
}
