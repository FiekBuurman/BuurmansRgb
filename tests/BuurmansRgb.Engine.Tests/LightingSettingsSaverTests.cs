using BuurmansRgb.Contracts;
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
    private readonly SettingsSaveEvents _saveEvents = new();

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
        var saver = new LightingSettingsSaver(_store, _lighting, new EngineSettings { SimulatedDevices = SimulatedDevicesMode.Never }, Delay, _time);
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.Equal(SimulatedDevicesMode.Never, _store.Settings.SimulatedDevices);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public async Task ChangeOptions_SavesTheOptionWithTheLightingAfterDelay()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.ChangeOptions(settings => settings with { SimulatedDevices = SimulatedDevicesMode.Always });
        _time.Advance(Delay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, _store.SaveCount);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(SimulatedDevicesMode.Always, _store.Settings.SimulatedDevices);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    [Fact]
    public async Task ChangeOptions_IsKeptForTheNextLightingChange()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        saver.ChangeOptions(settings => settings with { SimulatedDevices = SimulatedDevicesMode.Never });
        _time.Advance(Delay);

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        Assert.Equal(2, _store.SaveCount);
        Assert.Equal(SimulatedDevicesMode.Never, _store.Settings.SimulatedDevices);
    }

    [Fact]
    public void ChangeOptions_AfterDispose_SavesNothing()
    {
        var saver = CreateSaver();
        saver.Dispose();

        saver.ChangeOptions(settings => settings with { SimulatedDevices = SimulatedDevicesMode.Never });
        _time.Advance(Delay);

        Assert.Equal(0, _store.SaveCount);
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

    [Fact]
    public async Task Save_TellsTheSaveEvents()
    {
        await _lighting.DiscoverAsync(Ct);
        var saves = new List<bool>();
        _saveEvents.Saved += (_, succeeded) => saves.Add(succeeded);
        using var saver = CreateSaver();

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        Assert.Equal([true], saves);
    }

    [Fact]
    public async Task SaveFails_TellsTheSaveEventsItFailed()
    {
        await _lighting.DiscoverAsync(Ct);
        var saves = new List<bool>();
        _saveEvents.Saved += (_, succeeded) => saves.Add(succeeded);
        _store.SaveException = new IOException("Disk full.");
        using var saver = CreateSaver();

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        Assert.Equal([false], saves);
    }

    [Fact]
    public void NothingToSave_TellsNothing()
    {
        var saves = 0;
        _saveEvents.Saved += (_, _) => saves++;

        CreateSaver().Dispose();

        Assert.Equal(0, saves);
    }

    // The same content as the last save: the file holds it already.
    [Fact]
    public async Task ChangeBackBeforeTheNextSave_WritesNothing()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        _lighting.SetLighting(null, lighting => lighting with { Color = Red });
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        Assert.Equal(1, _store.SaveCount);
    }

    // A change by hand that the engine took as it is: the file keeps the user's words.
    [Fact]
    public async Task Adopt_WhatTheChangeByHandChanges_IsNotWrittenBack()
    {
        await _lighting.DiscoverAsync(Ct);
        var saver = CreateSaver();
        var fromFile = new EngineSettings { Color = Orange, FramesPerSecond = 12 };

        ApplyByHand(saver, fromFile, () =>
        {
            saver.ChangeOptions(settings => settings with { FramesPerSecond = 12 });
            _lighting.Replace(fromFile.ToLightingState());
        });
        _time.Advance(Delay);
        saver.Dispose();

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Adopt_AChangeAfterwards_IsSavedWithTheAdoptedSettings()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        ApplyByHand(saver, new EngineSettings { FramesPerSecond = 12 }, () => saver.ChangeOptions(settings => settings with { FramesPerSecond = 12 }));

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(12, _store.Settings.FramesPerSecond);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // The scene went off with the new lighting, while the file still names it: saved, or it would
    // be on again after a restart.
    [Fact]
    public async Task Adopt_SceneThatWentOff_IsSaved()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = new LightingSettingsSaver(_store, _lighting, new EngineSettings { ActiveScene = "Nacht" }, Delay, _time);
        var fromFile = new EngineSettings { Color = Orange, ActiveScene = "Nacht" };

        ApplyByHand(saver, fromFile, () =>
        {
            _lighting.Replace(fromFile.ToLightingState());
            saver.ChangeOptions(settings => settings with { ActiveScene = null });
        });
        _time.Advance(Delay);

        Assert.Equal(1, _store.SaveCount);
        Assert.Null(_store.Settings.ActiveScene);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // A save of a change before, due while the change by hand is applied: it waits, and then the
    // file holds what the engine has.
    [Fact]
    public async Task Adopt_SaveThatComesDueMeanwhile_WaitsAndWritesNothing()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        _lighting.SetLighting(null, lighting => lighting with { Color = Red });
        var fromFile = new EngineSettings { Color = Orange };

        ApplyByHand(saver, fromFile, () =>
        {
            _time.Advance(Delay);
            Assert.Equal(0, _store.SaveCount);
            _lighting.Replace(fromFile.ToLightingState());
        });
        _time.Advance(Delay);

        Assert.Equal(0, _store.SaveCount);
    }

    // Such as the settings app, while the change by hand is applied: not in the file, so saved.
    [Fact]
    public async Task Adopt_ChangeOnAnotherThreadMeanwhile_IsSaved()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        var fromFile = new EngineSettings { FramesPerSecond = 12 };

        ApplyByHand(saver, fromFile, () =>
        {
            saver.ChangeOptions(settings => settings with { FramesPerSecond = 12 });
            Task.Run(() => _lighting.SetLighting(null, lighting => lighting with { Color = Orange }), Ct).Wait(Ct);
        });
        _time.Advance(Delay);

        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(12, _store.Settings.FramesPerSecond);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // A change in the app on another thread that is on the lights once the file is, while its
    // LightingChanged only comes after HoldSaves: not taken as in the file, so saved.
    [Fact]
    public async Task Adopt_ChangeOnAnotherThreadWhoseEventComesLater_IsSaved()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        var fromFile = new EngineSettings { Color = Orange };
        var appThread = 0;
        using var changed = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        // Rendered comes between the change and its LightingChanged.
        _lighting.Rendered += (_, _) =>
        {
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref appThread))
            {
                changed.Set();
                release.Wait(Ct);
            }
        };
        Task? app = null;

        saver.HoldSaves(() =>
        {
            var applied = _lighting.Replace(fromFile.ToLightingState());
            app = Task.Factory.StartNew(
                () =>
                {
                    Volatile.Write(ref appThread, Environment.CurrentManagedThreadId);
                    _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.5 });
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            changed.Wait(Ct);
            saver.Adopt(fromFile, applied);
        });
        release.Set();
        await app!;
        _time.Advance(Delay);

        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
        Assert.Equal(0.5, _store.Settings.Brightness);
    }

    // The file changed by hand and nobody read it yet: the save waits for the reloader.
    [Fact]
    public async Task FileChangedByHandNotReadYet_SavesOnceItIsRead()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);
        Assert.Equal(0, _store.SaveCount);

        _store.LoadIfChanged();
        _time.Advance(Delay);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // Read and applied by the reloader meanwhile: the engine holds what the file holds.
    [Fact]
    public async Task FileChangedByHandNotReadYet_AdoptedThen_WritesNothing()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        var fromFile = new EngineSettings { FramesPerSecond = 12 };
        _store.ChangeByHand(fromFile);
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _time.Advance(Delay);

        ApplyByHand(saver, fromFile, () =>
        {
            _store.LoadIfChanged();
            saver.ChangeOptions(settings => settings with { FramesPerSecond = 12 });
            _lighting.Replace(fromFile.ToLightingState());
        });
        _time.Advance(Delay);

        Assert.Equal(0, _store.SaveCount);
        Assert.Same(fromFile, _store.Settings);
    }

    // Without a watcher nobody reads the change: after a few tries the engine saves anyway.
    [Fact]
    public async Task FileChangedByHandThatNobodyReads_IsSavedOverAfterAFewTries()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        for (var i = 0; i < 4; i++)
        {
            _time.Advance(Delay);
        }

        Assert.Equal(0, _store.SaveCount);
        _time.Advance(Delay);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // At the exit the change by hand goes before what waited.
    [Fact]
    public async Task Dispose_FileChangedByHandNotReadYet_WritesNothing()
    {
        await _lighting.DiscoverAsync(Ct);
        var saver = CreateSaver();
        var byHand = new EngineSettings { FramesPerSecond = 12 };
        _store.ChangeByHand(byHand);
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.Dispose();

        Assert.Equal(0, _store.SaveCount);
        Assert.Same(byHand, _store.Settings);
    }

    // The reloader read the file and found nothing new: a save that came due meanwhile goes on.
    [Fact]
    public async Task HoldSaves_WithoutAChangeByHand_SavesWhatCameDueAfterwards()
    {
        await _lighting.DiscoverAsync(Ct);
        using var saver = CreateSaver();
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        saver.HoldSaves(() => _time.Advance(Delay));
        Assert.Equal(0, _store.SaveCount);

        _time.Advance(Delay);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // As SettingsReloader does: apply within HoldSaves, then Adopt. Single-threaded, the lighting
    // as the apply left it is the one the engine holds.
    private void ApplyByHand(LightingSettingsSaver saver, EngineSettings fromFile, Action apply) =>
        saver.HoldSaves(() =>
        {
            apply();
            saver.Adopt(fromFile, _lighting.State);
        });

    private LightingSettingsSaver CreateSaver() => new(_store, _lighting, EngineSettings.CreateDefault(), Delay, _time, _saveEvents);
}
