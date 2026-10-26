using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class CustomEffectLibraryTests : IDisposable
{
    private readonly InMemoryCustomEffectStore _store = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly CustomEffectLibrary _library;

    public CustomEffectLibraryTests()
    {
        _lighting = new LightingController([_provider]);
        _library = new CustomEffectLibrary(_store, _lighting);
    }

    public void Dispose() => _lighting.Dispose();

    [Fact]
    public void Save_NewEffect_GetsAnIdAndIsStored()
    {
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        var saved = _library.Save(Effect("", " Disco "));

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

    // The list only changes once the file is written.
    [Fact]
    public void Save_StoreFails_KeepsTheList()
    {
        _store.SaveException = new IOException("in use");

        Assert.Throws<IOException>(() => _library.Save(Effect("", "Disco")));
        Assert.Empty(_library.Effects);
    }

    [Fact]
    public void Delete_RemovesTheEffect()
    {
        var disco = _library.Save(Effect("", "Disco"));
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _library.Delete(disco.Id);

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

    private static CustomEffect Effect(string id, string name) => new(id, name, [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);
}
