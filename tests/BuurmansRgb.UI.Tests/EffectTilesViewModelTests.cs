using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The effect tiles on Kleur (DESIGN.md § 9.3): a tile per effect of the engine and "Eigen effect" as
// the last, which chooses the own effect used last, or without own effects nothing (besluit 10).
public sealed class EffectTilesViewModelTests
{
    private static readonly EffectOption Static = EffectOption.Create(EffectIds.Static);

    private static readonly EffectOption Rainbow = EffectOption.Create(EffectIds.Rainbow);

    private static readonly EffectOption Avond = new("custom:avond", "Avond", 1);

    private static readonly EffectOption Disco = new("custom:disco", "Disco", 2);

    [Fact]
    public void Show_GivesATilePerEffectAndEigenEffectAsTheLast()
    {
        var tiles = new EffectTilesViewModel();

        tiles.Show([Static, Rainbow, Avond, Disco], Rainbow);

        Assert.Equal([Strings.Effect_static, Strings.Effect_rainbow, Strings.Effect_custom], tiles.Tiles.Select(tile => tile.DisplayName));
        Assert.Equal([(1, 3), (2, 3), (3, 3)], tiles.Tiles.Select(tile => (tile.Position, tile.SetSize)));
        Assert.Equal([false, true, false], tiles.Tiles.Select(tile => tile.IsChosen));
        Assert.Same(tiles.Tiles[1], tiles.ChosenTile);
        Assert.Equal(Strings.EffectDescription_rainbow, tiles.Tiles[1].HelpText);
        Assert.Equal([Avond, Disco], tiles.CustomEffects);
        Assert.Null(tiles.SelectedCustomEffect);
    }

    // Before the engine said its effects, there are no tiles, also no "Eigen effect".
    [Fact]
    public void Show_WithoutEffects_GivesNoTiles()
    {
        var tiles = new EffectTilesViewModel();

        tiles.Show([], null);

        Assert.Empty(tiles.Tiles);
        Assert.Null(tiles.ChosenTile);
    }

    // An own effect that comes or goes keeps the tiles, so the one with the keyboard focus stays.
    [Fact]
    public void Show_AnotherOwnEffect_KeepsTheTiles()
    {
        var tiles = new EffectTilesViewModel();
        tiles.Show([Static, Rainbow], Static);
        var before = tiles.Tiles.ToList();

        tiles.Show([Static, Rainbow, Disco], Static);

        Assert.Equal(before, tiles.Tiles);
        Assert.True(tiles.HasCustomEffects);
        Assert.True(tiles.Tiles[2].CanChoose);
    }

    // An own effect chosen: "Eigen effect" is the chosen tile, and the choice in card 4 shows it.
    [Fact]
    public void ShowChosen_AnOwnEffect_ChoosesTheTileEigenEffect()
    {
        var tiles = new EffectTilesViewModel();
        tiles.Show([Static, Rainbow, Avond, Disco], Static);

        tiles.ShowChosen(Disco);

        Assert.Equal([false, false, true], tiles.Tiles.Select(tile => tile.IsChosen));
        Assert.Same(Disco, tiles.SelectedCustomEffect);
    }

    [Fact]
    public void Choose_AnEffectOfTheEngine_AsksForIt()
    {
        var tiles = new EffectTilesViewModel();
        tiles.Show([Static, Rainbow], Static);
        var chosen = new List<EffectOption>();
        tiles.EffectChosen += (_, effect) => chosen.Add(effect);

        tiles.ChooseCommand.Execute(tiles.Tiles[1]);

        Assert.Equal([Rainbow], chosen);
    }

    // "Eigen effect" chooses the own effect used last while the app runs, else the first of the list.
    [Fact]
    public void Choose_EigenEffect_ChoosesTheOwnEffectUsedLast()
    {
        var tiles = new EffectTilesViewModel();
        tiles.Show([Static, Avond, Disco], Static);
        var chosen = new List<EffectOption>();
        tiles.EffectChosen += (_, effect) => chosen.Add(effect);
        Assert.Equal(UiText.Format(Strings.EffectTileCustomHelp, "Avond"), tiles.Tiles[1].HelpText);

        tiles.ChooseCommand.Execute(tiles.Tiles[1]);
        tiles.ShowChosen(Disco);
        tiles.ShowChosen(Static);
        tiles.ChooseCommand.Execute(tiles.Tiles[1]);

        Assert.Equal([Avond, Disco], chosen);
        Assert.Equal(UiText.Format(Strings.EffectTileCustomHelp, "Disco"), tiles.Tiles[1].HelpText);
    }

    // Without own effects "Eigen effect" chooses nothing: it asks only for "Jouw effect" in card 4.
    [Fact]
    public void Choose_EigenEffectWithoutOwnEffects_AsksOnlyForTheCard()
    {
        var tiles = new EffectTilesViewModel();
        tiles.Show([Static, Rainbow], Rainbow);
        var chosen = new List<EffectOption>();
        var cards = 0;
        tiles.EffectChosen += (_, effect) => chosen.Add(effect);
        tiles.CustomCardRequested += (_, _) => cards++;
        var custom = tiles.Tiles[2];

        tiles.ChooseCommand.Execute(custom);

        Assert.False(custom.CanChoose);
        Assert.Equal(Strings.EffectTileCustomNoneHelp, custom.HelpText);
        Assert.Empty(chosen);
        Assert.Equal(1, cards);
    }

    // The choice in card 4 asks for the own effect; null, as a list made anew gives, asks nothing.
    [Fact]
    public void SelectedCustomEffect_AsksForTheOwnEffect()
    {
        var tiles = new EffectTilesViewModel();
        tiles.Show([Static, Avond, Disco], Avond);
        var chosen = new List<EffectOption>();
        tiles.EffectChosen += (_, effect) => chosen.Add(effect);

        tiles.SelectedCustomEffect = Disco;
        tiles.SelectedCustomEffect = null;
        tiles.SelectedCustomEffect = Avond;

        Assert.Equal([Disco], chosen);
    }
}
