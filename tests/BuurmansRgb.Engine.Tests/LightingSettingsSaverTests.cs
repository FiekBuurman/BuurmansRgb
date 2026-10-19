using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingSettingsSaverTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(200);

    private readonly InMemorySettingsStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task LightingChanged_SavesAfterDelay()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, _store.SaveCount);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public async Task ManyQuickChanges_SaveOnceWithTheLatest()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();

        for (var i = 1; i <= 10; i++)
        {
            var brightness = i / 10.0;
            _lighting.SetLighting(null, lighting => lighting with { Brightness = brightness });
            _time.Advance(Delay / 2);
        }

        Assert.Equal(0, _store.SaveCount);
        _time.Advance(Delay);
        _time.Advance(Delay);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(1.0, _store.Settings.Brightness);
    }

    [Fact]
    public async Task Dispose_WithPendingChange_SavesImmediately()
    {
        await _lighting.DiscoverAsync(Ct);
        var saver = CreateSaver();
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public void Dispose_WithoutChanges_DoesNotSave()
    {
        CreateSaver().Dispose();

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task ChangeAfterDispose_IsNotSaved()
    {
        await _lighting.DiscoverAsync(Ct);
        CreateSaver().Dispose();

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task SaveFails_DoesNotThrow()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.SaveException = new IOException("Disk full.");
        var saver = CreateSaver();
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Save_KeepsTheStartupOptions()
    {
        await _lighting.DiscoverAsync(Ct);
        var saver = new LightingSettingsSaver(_store, _lighting, new EngineSettings { SimulatedDevices = false }, Delay, _time);
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.False(_store.Settings.SimulatedDevices);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public async Task DefaultTimeProvider_SavesOnItsOwn()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = new LightingSettingsSaver(_store, _lighting, EngineSettings.CreateDefault(), TimeSpan.FromMilliseconds(1));

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_store.SaveCount == 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the save.");
            await Task.Delay(10, Ct);
        }
    }

    private LightingSettingsSaver CreateSaver() => new(_store, _lighting, EngineSettings.CreateDefault(), Delay, _time);
}
