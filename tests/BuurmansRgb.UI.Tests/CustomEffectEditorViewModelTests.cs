using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class CustomEffectEditorViewModelTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep([Green, Blue], 2, 0.5)]);

    private readonly List<CustomEffect> _changes = [];
    private readonly CustomEffectEditorViewModel _editor;

    public CustomEffectEditorViewModelTests()
    {
        _editor = new CustomEffectEditorViewModel((id, name) => id != "custom:feest" && name == "Feest");
        _editor.Changed += (_, effect) => _changes.Add(effect);
    }

    [Fact]
    public void Show_ShowsTheNameStepsAndTheFirstStepWithoutSending()
    {
        _editor.Show(Disco);

        Assert.Equal("Disco", _editor.Name);
        Assert.Equal([.. Disco.Steps], _editor.Steps.Select(step => step.Step));
        Assert.True(_editor.Steps[0].IsSelected);
        Assert.Equal((Red, 1.0, 0.0), (_editor.SelectedColor, _editor.HoldSeconds, _editor.FadeSeconds));
        Assert.Equal(UiText.Format(Strings.CustomEffectSelectedStep, 1, 2, "#FF0000"), _editor.SelectedStepText);
        Assert.Empty(_changes);
    }

    [Fact]
    public void SelectStep_ShowsItsColorAndTimes()
    {
        _editor.Show(Disco);

        _editor.SelectStepCommand.Execute(1);

        Assert.Equal((Green, 2.0, 0.5), (_editor.SelectedColor, _editor.HoldSeconds, _editor.FadeSeconds));
        Assert.True(_editor.Steps[1].IsSelected);
        Assert.False(_editor.Steps[0].IsSelected);
        Assert.Equal(UiText.Format(Strings.CustomEffectSelectedStep, 2, 2, UiText.Format(Strings.CustomEffectPattern, 2)), _editor.SelectedStepText);
        Assert.Empty(_changes);
    }

    // A pattern becomes one color for the whole device.
    [Fact]
    public void Paint_GivesTheSelectedStepThatColor()
    {
        _editor.Show(Disco);
        _editor.SelectStepCommand.Execute(1);

        _editor.Paint(Orange);

        Assert.Equal([Orange], _editor.Steps[1].Colors);
        Assert.Equal(Disco with { Steps = [Disco.Steps[0], new CustomEffectStep(Orange, 2, 0.5)] }, Assert.Single(_changes));
    }

    [Fact]
    public void ShowPattern_MakesTheSelectedStepAPattern()
    {
        _editor.Show(Disco);

        _editor.ShowPattern([Red, Green, Blue]);

        Assert.Equal([Red, Green, Blue], Assert.Single(_changes).Steps[0].Colors);
        Assert.True(_editor.Steps[0].Step.IsPattern);
    }

    // In tenths of a second, within the range.
    [Theory]
    [InlineData(2.345, 2.3)]
    [InlineData(-1.0, 0.0)]
    [InlineData(75.0, CustomEffects.MaxSeconds)]
    public void HoldSeconds_ChangesTheSelectedStep(double seconds, double expected)
    {
        _editor.Show(Disco);

        _editor.HoldSeconds = seconds;

        Assert.Equal(expected, Assert.Single(_changes).Steps[0].HoldSeconds);
    }

    [Fact]
    public void FadeSeconds_ChangesTheSelectedStep()
    {
        _editor.Show(Disco);
        _editor.SelectStepCommand.Execute(1);

        _editor.FadeSeconds = 4;

        Assert.Equal(4.0, Assert.Single(_changes).Steps[1].FadeSeconds);
        Assert.Equal(UiText.Format(Strings.CustomEffectSeconds, 4.0), _editor.FadeText);
    }

    // The new step comes after the selected one, as a copy of it, and is selected.
    [Fact]
    public void AddStep_CopiesTheSelectedStepAfterIt()
    {
        _editor.Show(Disco);

        _editor.AddStepCommand.Execute(null);

        Assert.Equal([Disco.Steps[0], Disco.Steps[0], Disco.Steps[1]], Assert.Single(_changes).Steps);
        Assert.Equal(1, _editor.SelectedIndex);
        Assert.Equal(3, _editor.Steps.Count);
    }

    [Fact]
    public void AddStep_TheMostSteps_CannotRun()
    {
        _editor.Show(Disco with { Steps = [.. Enumerable.Repeat(new CustomEffectStep(Red), CustomEffects.MaxSteps)] });

        Assert.False(_editor.AddStepCommand.CanExecute(null));
    }

    // The step after it is selected; the last step cannot go.
    [Fact]
    public void RemoveStep_TakesTheSelectedStepAway()
    {
        _editor.Show(Disco);

        _editor.RemoveStepCommand.Execute(null);

        Assert.Equal([Disco.Steps[1]], Assert.Single(_changes).Steps);
        Assert.Equal(0, _editor.SelectedIndex);
        Assert.Equal(Green, _editor.SelectedColor);
        Assert.False(_editor.RemoveStepCommand.CanExecute(null));
    }

    [Fact]
    public void MoveStep_SwapsWithTheNeighbourAndKeepsItSelected()
    {
        _editor.Show(Disco);
        Assert.False(_editor.MoveStepEarlierCommand.CanExecute(null));

        _editor.MoveStepLaterCommand.Execute(null);

        Assert.Equal([Disco.Steps[1], Disco.Steps[0]], _changes[0].Steps);
        Assert.Equal(1, _editor.SelectedIndex);
        Assert.Equal(Red, _editor.SelectedColor);
        Assert.False(_editor.MoveStepLaterCommand.CanExecute(null));

        _editor.MoveStepEarlierCommand.Execute(null);

        Assert.Equal([.. Disco.Steps], _changes[1].Steps);
        Assert.Equal(0, _editor.SelectedIndex);
    }

    [Fact]
    public void Name_Valid_IsTrimmedAndSent()
    {
        _editor.Show(Disco);

        _editor.Name = "  Avond  ";

        Assert.Equal("Avond", _editor.Name);
        Assert.Equal(Disco with { Name = "Avond" }, Assert.Single(_changes));
        Assert.Null(_editor.NameError);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Feest")]
    public void Name_EmptyOrOfAnotherEffect_SaysWhyAndSendsNothing(string name)
    {
        _editor.Show(Disco);

        _editor.Name = name;

        Assert.Empty(_changes);
        Assert.NotNull(_editor.NameError);
        Assert.Equal("Disco", _editor.Effect.Name);
    }

    // When the box loses the focus, it shows the name the effect has again, and the message says
    // which name was refused and that the effect keeps its name; also after Enter showed why.
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", true)]
    [InlineData(" Feest ", false)]
    [InlineData("Feest", true)]
    public void EndNameEdit_NameNotKept_ShowsTheNameOfTheEffectAndSaysItStays(string text, bool enterFirst)
    {
        _editor.Show(Disco);
        if (enterFirst)
        {
            _editor.Name = text;
        }

        _editor.EndNameEdit(text);

        Assert.Equal("Disco", _editor.Name);
        Assert.Equal(
            text.Trim().Length == 0 ? UiText.Format(Strings.CustomEffectNameEmptyKept, "Disco") : UiText.Format(Strings.CustomEffectNameTakenKept, "Feest", "Disco"),
            _editor.NameError);
        Assert.Empty(_changes);
    }

    // A valid name that the box still had keeps it, as Enter does.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndNameEdit_NameKept_KeepsIt(bool enterFirst)
    {
        _editor.Show(Disco);
        _editor.Name = "";
        if (enterFirst)
        {
            _editor.Name = " Avond ";
        }

        _editor.EndNameEdit(" Avond ");

        Assert.Equal("Avond", _editor.Name);
        Assert.Null(_editor.NameError);
        Assert.Equal(["Avond"], _changes.Select(effect => effect.Name));
    }

    // The name of the effect itself, also with spaces around it, is no other name.
    [Fact]
    public void EndNameEdit_NameOfTheEffect_ShowsItWithoutAMessage()
    {
        _editor.Show(Disco);

        _editor.EndNameEdit(" Disco ");

        Assert.Equal("Disco", _editor.Name);
        Assert.Null(_editor.NameError);
        Assert.Empty(_changes);
    }

    // Escape puts back the name of the effect, also after Enter refused one, and drops the message.
    [Theory]
    [InlineData("Feest")]
    [InlineData("")]
    public void CancelNameEdit_ShowsTheNameOfTheEffectWithoutAMessage(string refused)
    {
        _editor.Show(Disco);
        _editor.Name = refused;

        _editor.CancelNameEdit();

        Assert.Equal("Disco", _editor.Name);
        Assert.Null(_editor.NameError);
        Assert.Empty(_changes);
    }

    // The same name refused again is news too, for a screen reader.
    [Fact]
    public void EndNameEdit_SameNameRefusedAgain_TellsItAgain()
    {
        _editor.Show(Disco);
        _editor.EndNameEdit("Feest");
        var told = 0;
        _editor.PropertyChanged += (_, e) => told += e.PropertyName == nameof(CustomEffectEditorViewModel.NameError) ? 1 : 0;

        _editor.EndNameEdit("Feest");

        Assert.Equal(1, told);
    }

    // Once the name made way, the box follows the effect again, such as another settings app that
    // renames it; the message named the name the effect had, so it goes.
    [Fact]
    public void Show_SameEffectAfterTheNameMadeWay_ShowsItsNewNameWithoutTheMessage()
    {
        _editor.Show(Disco);
        _editor.EndNameEdit("");

        _editor.Show(Disco with { Name = "Avond" });

        Assert.Equal("Avond", _editor.Name);
        Assert.Null(_editor.NameError);
    }

    // Another change of the effect, with the same name, leaves the message.
    [Fact]
    public void Show_SameEffectAndNameAfterTheNameMadeWay_KeepsTheMessage()
    {
        _editor.Show(Disco);
        _editor.EndNameEdit("");

        _editor.Show(Disco with { Steps = [new CustomEffectStep(Blue)] });

        Assert.Equal(UiText.Format(Strings.CustomEffectNameEmptyKept, "Disco"), _editor.NameError);
    }

    // The same message for another name that is not kept is news too, for a screen reader.
    [Fact]
    public void Name_SameMessageAgain_TellsItAgain()
    {
        _editor.Show(Disco);
        _editor.Name = "";
        var told = 0;
        _editor.PropertyChanged += (_, e) => told += e.PropertyName == nameof(CustomEffectEditorViewModel.NameError) ? 1 : 0;

        _editor.Name = " ";

        Assert.Equal(1, told);
        Assert.Equal(Strings.CustomEffectNameInvalid, _editor.NameError);
    }

    // A valid name again ends the message.
    [Fact]
    public void Name_ValidAfterAnInvalidOne_EndsTheMessage()
    {
        _editor.Show(Disco);
        _editor.Name = "";

        _editor.Name = "Avond";

        Assert.Null(_editor.NameError);
        Assert.Equal("Avond", Assert.Single(_changes).Name);
    }

    // The same effect from the engine keeps the selection; a name being typed stays while it is
    // not valid.
    [Fact]
    public void Show_SameEffectAgain_KeepsTheSelectionAndTheNameBeingTyped()
    {
        _editor.Show(Disco);
        _editor.SelectStepCommand.Execute(1);
        _editor.Name = "";

        _editor.Show(Disco with { Steps = [.. Disco.Steps, new CustomEffectStep(Blue)] });

        Assert.Equal(1, _editor.SelectedIndex);
        Assert.Equal("", _editor.Name);
        Assert.Equal(3, _editor.Steps.Count);
    }

    [Fact]
    public void Show_AnotherEffect_SelectsItsFirstStep()
    {
        _editor.Show(Disco);
        _editor.SelectStepCommand.Execute(1);

        _editor.Show(Disco with { Id = "custom:feest", Name = "Feest" });

        Assert.Equal(0, _editor.SelectedIndex);
        Assert.Equal("Feest", _editor.Name);
    }

    // The tiles that stay are the same objects, so a tile with the focus keeps it.
    [Fact]
    public void Paint_KeepsTheTiles()
    {
        _editor.Show(Disco);
        var tiles = _editor.Steps.ToArray();

        _editor.Paint(Blue);
        _editor.AddStepCommand.Execute(null);

        Assert.Same(tiles[0], _editor.Steps[0]);
        Assert.Same(tiles[1], _editor.Steps[1]);
    }

    [Fact]
    public void Delete_AsksForTheEffect()
    {
        _editor.Show(Disco);
        string? deleted = null;
        _editor.DeleteRequested += (_, id) => deleted = id;

        _editor.DeleteCommand.Execute(null);

        Assert.Equal(Disco.Id, deleted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Delete_AsksForConfirmationFirst(bool confirmed)
    {
        _editor.Show(Disco);
        string? asked = null;
        _editor.ConfirmDelete = name =>
        {
            asked = name;
            return confirmed;
        };
        var deleted = new List<string>();
        _editor.DeleteRequested += (_, id) => deleted.Add(id);

        _editor.DeleteCommand.Execute(null);

        Assert.Equal("Disco", asked);
        Assert.Equal(confirmed ? [Disco.Id] : [], deleted);
    }

    // The question waits for the user while the app goes on, so another app can choose another
    // effect meanwhile: the yes still deletes the effect that was asked about.
    [Fact]
    public void Delete_OtherEffectWhileAsking_DeletesTheOneAskedAbout()
    {
        _editor.Show(Disco);
        _editor.ConfirmDelete = _ =>
        {
            _editor.Show(new CustomEffect("custom:feest", "Feest", [new CustomEffectStep(Blue, 1, 0)]));
            return true;
        };
        string? deleted = null;
        _editor.DeleteRequested += (_, id) => deleted = id;

        _editor.DeleteCommand.Execute(null);

        Assert.Equal(Disco.Id, deleted);
    }

    [Fact]
    public void StepItem_NameSaysTheStepAndItsTimes()
    {
        _editor.Show(Disco);

        Assert.Equal(UiText.Format(Strings.CustomEffectStepName, 1, "#FF0000", 1.0, 0.0), _editor.Steps[0].Name);
        Assert.Equal(UiText.Format(Strings.CustomEffectStepName, 2, UiText.Format(Strings.CustomEffectPattern, 2), 2.0, 0.5), _editor.Steps[1].ToString());
    }
}
