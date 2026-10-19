using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingSettingsSaverTests
{
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly InMemorySettingsStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);

    [Fact]
    public async Task LightingChanged_SavesAfterDelay()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        using var saver = new LightingSettingsSaver(_store, _lighting, TimeSpan.FromMilliseconds(20));

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        await WaitUntilAsync(() => _store.SaveCount == 1);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public async Task ManyQuickChanges_SaveOnceWithTheLatest()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        using var saver = new LightingSettingsSaver(_store, _lighting, TimeSpan.FromMilliseconds(200));

        for (var i = 1; i <= 10; i++)
        {
            var brightness = i / 10.0;
            _lighting.SetLighting(null, lighting => lighting with { Brightness = brightness });
        }

        await WaitUntilAsync(() => _store.SaveCount > 0);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(1.0, _store.Settings.Brightness);
    }

    [Fact]
    public async Task Dispose_WithPendingChange_SavesImmediately()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        var saver = new LightingSettingsSaver(_store, _lighting, TimeSpan.FromHours(1));
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public void Dispose_WithoutChanges_DoesNotSave()
    {
        new LightingSettingsSaver(_store, _lighting, TimeSpan.FromHours(1)).Dispose();

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task ChangeAfterDispose_IsNotSaved()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        new LightingSettingsSaver(_store, _lighting, TimeSpan.FromMilliseconds(1)).Dispose();

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task SaveFails_DoesNotThrow()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        _store.SaveException = new IOException("Disk full.");
        var saver = new LightingSettingsSaver(_store, _lighting, TimeSpan.FromHours(1));
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.Equal(0, _store.SaveCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the save.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
