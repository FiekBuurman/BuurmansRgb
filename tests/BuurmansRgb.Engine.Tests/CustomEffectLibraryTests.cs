using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class CustomEffectLibraryTests : IDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryCustomEffectStore _store = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly CustomEffectLibrary _library;

    public CustomEffectLibraryTests()
    {
        _lighting = new LightingController([_provider]);
        _library = new CustomEffectLibrary(_store, _lighting, Delay, _time);
    }

    public void Dispose()
    {
        _library.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public void Save_NewEffect_GetsAnIdAndIsStored()
    {
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        var saved = _library.Save(Effect("", " Disco "));
        _time.Advance(Delay);

        Assert.True(CustomEffects.IsCustom(saved.Id));
        Assert.Equal("Disco", saved.Name);
        Assert.Equal([saved], _store.Effects);
        Assert.Equal([saved], _library.Effects);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Save_ExistingEffect_ReplacesIt()
    {
        var saved = _library.Save(Effect("", "Disco"));

        var renamed = _library.Save(saved with { Name = "Feest", Steps = [new CustomEffectStep(Blue)] });

        Assert.Equal(saved.Id, renamed.Id);
        Assert.Equal([renamed], _library.Effects);
    }

    [Fact]
    public void Effects_AreInAlphabeticalOrder()
    {
        _library.Save(Effect("", "zon"));
        _library.Save(Effect("", "Avond"));
        _library.Save(Effect("", "middag"));

        Assert.Equal(["Avond", "middag", "zon"], _library.Effects.Select(effect => effect.Name));
    }

    [Fact]
    public void Save_UnknownId_Throws()
    {
        Assert.Throws<ArgumentException>(() => _library.Save(Effect("custom:gone", "Disco")));
        Assert.Empty(_store.Effects);
    }

    // Names match without regard to case, but an effect may keep its own name in another case.
    [Fact]
    public void Save_NameOfAnotherEffect_Throws()
    {
        var disco = _library.Save(Effect("", "Disco"));
        _library.Save(Effect("", "Feest"));

        Assert.Throws<ArgumentException>(() => _library.Save(Effect("", "disco")));
        Assert.Throws<ArgumentException>(() => _library.Save(disco with { Name = "FEEST" }));
        Assert.Equal("DISCO", _library.Save(disco with { Name = "DISCO" }).Name);
    }

    // The effect choice would show the name twice; case and spaces around it do not matter.
    [Theory]
    [InlineData("Regenboog")]
    [InlineData("regenboog")]
    [InlineData(" REGENBOOG ")]
    [InlineData("per led")]
    [InlineData("Kleurenloop")]
    public void Save_NameOfABuiltInEffect_ThrowsAndStoresNothing(string name)
    {
        var disco = _library.Save(Effect("", "Disco"));

        Assert.Throws<ArgumentException>(() => _library.Save(Effect("", name)));
        Assert.Throws<ArgumentException>(() => _library.Save(disco with { Name = name }));
        _time.Advance(Delay);

        Assert.Equal(["Disco"], _library.Effects.Select(effect => effect.Name));
        Assert.Equal(["Disco"], _store.Effects.Select(effect => effect.Name));
    }

    [Fact]
    public void Save_NameThatOnlyBeginsAsABuiltInEffect_IsKept()
    {
        Assert.Equal("Regenboog zacht", _library.Save(Effect("", "Regenboog zacht")).Name);
    }

    // An effect of a file from before the engine refused such a name is kept under a name of its
    // own, next to one that has that name already.
    [Fact]
    public void Load_EffectWithTheNameOfABuiltInEffect_GetsAnOwnName()
    {
        using var trace = new TraceCapture();
        _store.Effects = [Effect("custom:a", "regenboog"), Effect("custom:b", "Regenboog (eigen)"), Effect("custom:c", "Vuur")];
        (string, string)[] renamed = [("custom:b", "Regenboog (eigen)"), ("custom:a", "regenboog (eigen) 2"), ("custom:c", "Vuur (eigen)")];

        _library.Load();

        Assert.Equal(renamed, _library.Effects.Select(effect => (effect.Id, effect.Name)));
        Assert.Contains(trace.Messages, message => message.Contains("'regenboog'", StringComparison.Ordinal) && message.Contains("'regenboog (eigen) 2'", StringComparison.Ordinal));
        Assert.Contains(trace.Messages, message => message.Contains("'Vuur'", StringComparison.Ordinal) && message.Contains("'Vuur (eigen)'", StringComparison.Ordinal));

        // The next change writes the file with the new names of all of them.
        Assert.Equal("Vuur (eigen)", _library.Save(_library.Effects[2] with { Steps = [new CustomEffectStep(Blue)] }).Name);
        _time.Advance(Delay);

        Assert.Equal(renamed, _store.Effects.OrderBy(effect => effect.Name, CustomEffects.NameComparer).Select(effect => (effect.Id, effect.Name)));
    }

    [Fact]
    public void Save_InvalidEffect_ThrowsAndStoresNothing()
    {
        Assert.Throws<ArgumentException>(() => _library.Save(new CustomEffect("", "Disco", [])));
        Assert.Empty(_store.Effects);
    }

    [Fact]
    public void Save_MoreThanTheMost_Throws()
    {
        for (var i = 0; i < CustomEffects.MaxEffects; i++)
        {
            _library.Save(Effect("", $"Effect {i}"));
        }

        Assert.Throws<ArgumentException>(() => _library.Save(Effect("", "Een te veel")));
    }

    // The list changes at once, and so does the lighting; the file a moment after the last change,
    // so dragging a slider of the editor writes it once.
    [Fact]
    public async Task Save_WritesTheFileAMomentAfterTheLastChange()
    {
        await _lighting.DiscoverAsync(Ct);
        var disco = _library.Save(Effect("", "Disco"));
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, disco.Id)));

        for (var i = 1; i <= 5; i++)
        {
            _time.Advance(Delay / 2);
            _library.Save(disco with { Steps = [new CustomEffectStep(Green, i, 0)] });
        }

        Assert.Equal(0, _store.SaveCount);
        Assert.Equal(5, _library.Effects[0].Steps[0].HoldSeconds);
        Assert.Equal([Green, Green, Green], _provider.LastColors);
        _time.Advance(Delay);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(5, _store.Effects[0].Steps[0].HoldSeconds);
    }

    // At the exit a change that waits is written at once.
    [Fact]
    public void Dispose_WritesAChangeThatWaits()
    {
        var disco = _library.Save(Effect("", "Disco"));

        _library.Dispose();

        Assert.Equal([disco], _store.Effects);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void Dispose_NothingWaits_WritesNothing()
    {
        _library.Save(Effect("", "Disco"));
        _time.Advance(Delay);

        _library.Dispose();

        Assert.Equal(1, _store.SaveCount);
    }

    // A file that cannot be written now keeps the change in memory; the next change, or the exit,
    // writes it.
    [Fact]
    public void Save_StoreFails_KeepsTheChangeAndWritesItAtTheExit()
    {
        _store.SaveException = new IOException("in use");
        var disco = _library.Save(Effect("", "Disco"));
        _time.Advance(Delay);
        Assert.Equal([disco], _library.Effects);
        Assert.Empty(_store.Effects);

        _store.SaveException = null;
        _library.Dispose();

        Assert.Equal([disco], _store.Effects);
    }

    // After a failed write the timer tries again, so a crash does not cost all since that try.
    [Fact]
    public void Save_StoreFails_WritesAgainAMomentLater()
    {
        _store.SaveException = new IOException("in use");
        var disco = _library.Save(Effect("", "Disco"));
        _time.Advance(Delay);
        Assert.Empty(_store.Effects);

        _store.SaveException = null;
        _time.Advance(Delay);

        Assert.Equal([disco], _store.Effects);
    }

    // A second change after a failed write: the file gets both.
    [Fact]
    public void Save_StoreFailsThenAnotherChange_WritesBoth()
    {
        _store.SaveException = new IOException("in use");
        var disco = _library.Save(Effect("", "Disco"));
        _time.Advance(Delay);
        _store.SaveException = null;

        var feest = _library.Save(Effect("", "Feest"));
        _time.Advance(Delay);

        Assert.Equal([disco, feest], _store.Effects);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void Delete_RemovesTheEffect()
    {
        var disco = _library.Save(Effect("", "Disco"));
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _library.Delete(disco.Id);
        _time.Advance(Delay);

        Assert.Empty(_library.Effects);
        Assert.Empty(_store.Effects);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Delete_UnknownId_Throws()
    {
        Assert.Throws<ArgumentException>(() => _library.Delete("custom:gone"));
    }

    // The lighting gets every list: a device that shows a deleted effect gets static.
    [Fact]
    public async Task Delete_EffectThatShows_GivesTheLightingStatic()
    {
        await _lighting.DiscoverAsync(Ct);
        var disco = _library.Save(Effect("", "Disco"));
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, disco.Id)));
        Assert.Equal([Red, Red, Red], _provider.LastColors);

        _library.Delete(disco.Id);

        Assert.Equal(EffectIds.Static, _lighting.GetLighting(null).EffectId);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    // Read at the start, before the lighting comes on, so it keeps the effect it shows.
    [Fact]
    public async Task Load_GivesTheEffectsToTheLighting()
    {
        _store.Effects = [Effect("custom:disco", "Disco")];
        await _lighting.DiscoverAsync(Ct);

        _library.Load();
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, "custom:disco")));

        Assert.Equal("custom:disco", _lighting.GetLighting(null).EffectId);
        Assert.Equal([Red, Red, Red], _provider.LastColors);
    }

    // A file another program holds: none for now, and the next question reads it again.
    [Fact]
    public void Load_FileOutOfReach_GivesNoneAndReadsAgainLater()
    {
        _store.Effects = [Effect("custom:disco", "Disco")];
        _store.LoadException = new IOException("in use");

        _library.Load();
        _store.LoadException = null;

        Assert.Equal(["Disco"], _library.Effects.Select(effect => effect.Name));
        Assert.Equal(2, _store.LoadCount);
    }

    // Without a settings app nobody asks for the effects: the library tries again by itself, until
    // it can read the file, and then the device shows its effect and the apps hear of it.
    [Fact]
    public async Task Load_FileOutOfReach_ReadsItAgainBeforeLong()
    {
        _store.Effects = [Effect("custom:disco", "Disco")];
        _store.LoadException = new IOException("in use");
        await _lighting.DiscoverAsync(Ct);
        _library.Load();
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, "custom:disco")));
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _time.Advance(CustomEffectLibrary.LoadRetryDelay);
        Assert.Equal(2, _store.LoadCount);
        Assert.Equal(0, changes);
        _store.LoadException = null;
        _time.Advance(CustomEffectLibrary.LoadRetryDelay);

        Assert.Equal([Red, Red, Red], _provider.LastColors);
        Assert.Equal(1, changes);
        _time.Advance(CustomEffectLibrary.LoadRetryDelay * 3);
        Assert.Equal(3, _store.LoadCount);
    }

    // A question that reads the file first tells the apps too, and the library stops trying.
    [Fact]
    public void Load_FileOutOfReach_ReadByAQuestion_TellsTheAppsAndStopsTrying()
    {
        _store.Effects = [Effect("custom:disco", "Disco")];
        _store.LoadException = new IOException("in use");
        _library.Load();
        _store.LoadException = null;
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        Assert.Single(_library.Effects);
        _time.Advance(CustomEffectLibrary.LoadRetryDelay * 3);

        Assert.Equal(1, changes);
        Assert.Equal(2, _store.LoadCount);
    }

    [Fact]
    public void Dispose_StopsTryingToRead()
    {
        _store.LoadException = new IOException("in use");
        _library.Load();

        _library.Dispose();
        _time.Advance(CustomEffectLibrary.LoadRetryDelay * 3);

        Assert.Equal(1, _store.LoadCount);
    }

    // A write that keeps failing logs its first failure whole, then only the message, and the
    // write that goes through at last.
    [Fact]
    public void Save_StoreKeepsFailing_LogsTheWholeExceptionOnce()
    {
        using var trace = new TraceCapture();
        var message = $"in use {Guid.NewGuid()}";
        _store.SaveException = new IOException(message);
        _library.Save(Effect("", "Disco"));

        _time.Advance(Delay);
        _time.Advance(Delay);
        _time.Advance(Delay);
        _store.SaveException = null;
        _time.Advance(Delay);

        var mine = trace.Messages.Where(line => line.Contains(message, StringComparison.Ordinal)).ToList();
        Assert.Equal(3, mine.Count);
        Assert.Contains(typeof(IOException).FullName!, mine[0], StringComparison.Ordinal);
        Assert.All(mine.Skip(1), line => Assert.StartsWith("Still could not write the own effects: ", line, StringComparison.Ordinal));
        Assert.Contains("Wrote the own effects after all.", trace.Messages);
    }

    // The same for a file that cannot be read at the start.
    [Fact]
    public void Load_FileStaysOutOfReach_LogsTheWholeExceptionOnce()
    {
        using var trace = new TraceCapture();
        var message = $"in use {Guid.NewGuid()}";
        _store.LoadException = new IOException(message);
        _library.Load();

        _time.Advance(CustomEffectLibrary.LoadRetryDelay);
        _time.Advance(CustomEffectLibrary.LoadRetryDelay);
        _store.LoadException = null;
        _time.Advance(CustomEffectLibrary.LoadRetryDelay);

        var mine = trace.Messages.Where(line => line.Contains(message, StringComparison.Ordinal)).ToList();
        Assert.Equal(3, mine.Count);
        Assert.Contains(typeof(IOException).FullName!, mine[0], StringComparison.Ordinal);
        Assert.All(mine.Skip(1), line => Assert.StartsWith("Still could not read the own effects: ", line, StringComparison.Ordinal));
        Assert.Contains("Read the own effects after all.", trace.Messages);
    }

    // A question after the exit that reads the file at last does not touch the stopped retry
    // timer: ITimer.Change may throw ObjectDisposedException then.
    [Fact]
    public void Dispose_ThenReadAfterAFailedLoad_LeavesTheTimerAlone()
    {
        var store = new InMemoryCustomEffectStore { Effects = [Effect("custom:disco", "Disco")], LoadException = new IOException("in use") };
        var library = new CustomEffectLibrary(store, _lighting, Delay, new StrictTimeProvider(_time));
        library.Load();
        library.Dispose();
        store.LoadException = null;

        Assert.Equal(["Disco"], library.Effects.Select(effect => effect.Name));
        library.Save(Effect("", "Feest"));
        Assert.Equal(["Disco", "Feest"], store.Effects.Select(effect => effect.Name));
    }

    // Timers that throw when they are changed after Dispose, as ITimer allows.
    private sealed class StrictTimeProvider(TimeProvider inner) : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new StrictTimer(inner.CreateTimer(callback, state, dueTime, period));

        private sealed class StrictTimer(ITimer inner) : ITimer
        {
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return inner.Change(dueTime, period);
            }

            public void Dispose()
            {
                _disposed = true;
                inner.Dispose();
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private static CustomEffect Effect(string id, string name) => new(id, name, [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);
}
