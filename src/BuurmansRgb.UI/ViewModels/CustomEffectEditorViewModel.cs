using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The panel "Eigen effect" on Kleur for the own effect that is chosen: its name, its steps as tiles
// of which one is selected, and how long that step stays and fades into the next. The color picker
// colors the selected step (Paint), and the LED colors of the device make it a pattern
// (ShowPattern). Each change the user makes goes out as the whole effect with Changed; Show shows
// the effect of the engine without sending it back. isNameTaken tells whether another effect, an
// own one with another id or one of the engine, has the name.
public sealed partial class CustomEffectEditorViewModel(Func<string, string, bool> isNameTaken) : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();
    private readonly List<CustomEffectStep> _steps = [];
    private string _id = string.Empty;

    // The name as it is kept: the last valid one.
    private string _name = string.Empty;

    // Raised with the whole effect after each change the user made.
    public event EventHandler<CustomEffect>? Changed;

    // Raised with the id of the effect when the user deletes it.
    public event EventHandler<string>? DeleteRequested;

    public ObservableCollection<CustomEffectStepItem> Steps { get; } = [];

    // The name in the text box; a valid one that no other effect has goes out when it is set.
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    // Why the name typed last is not kept; once the box shows the name of the effect again, which
    // name was refused and that the effect keeps its name. Null while it is kept.
    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedColor), nameof(SelectedStepText))]
    [NotifyCanExecuteChangedFor(nameof(MoveStepEarlierCommand), nameof(MoveStepLaterCommand))]
    public partial int SelectedIndex { get; private set; }

    // How long the selected step stays and fades, in seconds (CustomEffects.MinSeconds to MaxSeconds).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoldText))]
    public partial double HoldSeconds { get; set; } = CustomEffects.DefaultHoldSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FadeText))]
    public partial double FadeSeconds { get; set; } = CustomEffects.DefaultFadeSeconds;

    // The range of the times, for the sliders.
    public static double MinSeconds { get; } = CustomEffects.MinSeconds;

    public static double MaxSeconds { get; } = CustomEffects.MaxSeconds;

    public string HoldText => UiText.Format(Strings.CustomEffectSeconds, HoldSeconds);

    public string FadeText => UiText.Format(Strings.CustomEffectSeconds, FadeSeconds);

    // The id of the effect shown; empty before the first.
    public string EffectId => _id;

    // The color the picker shows: that of the selected step, or the first of its pattern.
    public RgbColor SelectedColor => _steps.Count == 0 ? RgbColor.Black : _steps[SelectedIndex].Colors[0];

    // "Stap 2 van 3 · #FF0000", or with "patroon van 104 LED's".
    public string SelectedStepText => _steps.Count == 0
        ? string.Empty
        : UiText.Format(Strings.CustomEffectSelectedStep, SelectedIndex + 1, _steps.Count, CustomEffectStepItem.Describe(_steps[SelectedIndex]));

    public CustomEffect Effect => new(_id, _name, [.. _steps]);

    // Asked before the effect is deleted, with its name; the view shows the question, which says
    // that devices and scenes with it become static. Without it, the effect is deleted without
    // asking.
    public Func<string, bool>? ConfirmDelete { get; set; }

    // Shows an effect of the engine. The selection stays on the same place for the same effect,
    // and goes to the first step for another one; a name being typed that is not valid stays, and
    // so does the message of one that made way (EndNameEdit), until the effect gets another name.
    public void Show(CustomEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (effect == Effect)
        {
            return;
        }

        _showing.Run(() =>
        {
            var same = effect.Id == _id;
            var typing = same && Name != _name;
            var renamed = same && effect.Name != _name;
            _id = effect.Id;
            _name = effect.Name;
            if (!typing)
            {
                Name = effect.Name;
            }

            // The message of a name that made way names the one the effect kept.
            if (!same || (renamed && !typing))
            {
                NameError = null;
            }

            _steps.Clear();
            _steps.AddRange(effect.Steps);
            Refresh(same ? Math.Min(SelectedIndex, _steps.Count - 1) : 0);
        });
    }

    // The text box lost the focus with this text in it. A valid name that no other effect has is
    // kept, as with Enter; one that is empty or that of another effect makes way for the name of
    // the effect, so the box shows what the effect choice does, and the message says which name
    // was refused and which one the effect keeps, until a valid name or another effect.
    public void EndNameEdit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var name = CustomEffects.NormalizeName(text);
        if (name is not null && (name == _name || !isNameTaken(_id, name)))
        {
            Name = text;
        }
        else
        {
            ShowNameError(name is null
                ? UiText.Format(Strings.CustomEffectNameEmptyKept, _name)
                : UiText.Format(Strings.CustomEffectNameTakenKept, name, _name));
        }

        if (Name != _name)
        {
            _showing.Run(() => Name = _name);
        }
    }

    // Escape in the text box: the box shows the name the effect has again, without a message, as
    // nothing was refused.
    public void CancelNameEdit()
    {
        _showing.Run(() => Name = _name);
        NameError = null;
    }

    // Gives the selected step one color: a pattern becomes that color for the whole device.
    // SelectedColor is not raised: the color picker that painted it shows it already.
    public void Paint(RgbColor color)
    {
        if (_steps.Count > 0)
        {
            ChangeStep(step => step with { Colors = [color] }, notifyColor: false);
        }
    }

    // Gives the selected step the colors of the LEDs of a device as its pattern.
    public void ShowPattern(IReadOnlyList<RgbColor> colors)
    {
        if (_steps.Count > 0 && colors.Count is > 0 and <= CustomEffects.MaxLeds)
        {
            ChangeStep(step => step with { Colors = [.. colors] }, notifyColor: true);
        }
    }

    [RelayCommand]
    private void SelectStep(int index)
    {
        if (index >= 0 && index < _steps.Count)
        {
            _showing.Run(() => Refresh(index));
        }
    }

    // The new step comes right after the selected one and starts as a copy of it, so the effect
    // looks the same until the user changes it.
    [RelayCommand(CanExecute = nameof(CanAddStep))]
    private void AddStep()
    {
        _steps.Insert(SelectedIndex + 1, _steps[SelectedIndex]);
        StepsChanged(SelectedIndex + 1);
    }

    private bool CanAddStep() => _steps.Count is > 0 and < CustomEffects.MaxSteps;

    // The step after it is selected then, or the one before when it was the last.
    [RelayCommand(CanExecute = nameof(CanRemoveStep))]
    private void RemoveStep()
    {
        _steps.RemoveAt(SelectedIndex);
        StepsChanged(Math.Min(SelectedIndex, _steps.Count - 1));
    }

    private bool CanRemoveStep() => _steps.Count > CustomEffects.MinSteps;

    [RelayCommand(CanExecute = nameof(CanMoveStepEarlier))]
    private void MoveStepEarlier() => MoveStep(SelectedIndex - 1);

    private bool CanMoveStepEarlier() => SelectedIndex > 0;

    [RelayCommand(CanExecute = nameof(CanMoveStepLater))]
    private void MoveStepLater() => MoveStep(SelectedIndex + 1);

    private bool CanMoveStepLater() => SelectedIndex < _steps.Count - 1;

    [RelayCommand]
    private void Delete()
    {
        // The effect that was asked about, also when another one shows while the question is open.
        var id = _id;
        if (id.Length > 0 && (ConfirmDelete?.Invoke(_name) ?? true))
        {
            DeleteRequested?.Invoke(this, id);
        }
    }

    partial void OnNameChanged(string value)
    {
        if (_showing.IsActive)
        {
            return;
        }

        var name = CustomEffects.NormalizeName(value);
        var error = name is null ? Strings.CustomEffectNameInvalid
            : isNameTaken(_id, name) ? Strings.CustomEffectNameTaken
            : null;
        ShowNameError(error);
        if (name is null || error is not null || name == _name)
        {
            return;
        }

        _name = name;
        _showing.Run(() => Name = name);
        OnChanged();
    }

    partial void OnHoldSecondsChanged(double value)
    {
        if (!_showing.IsActive && _steps.Count > 0)
        {
            ChangeStep(step => step with { HoldSeconds = Seconds(value) }, notifyColor: false);
        }
    }

    partial void OnFadeSecondsChanged(double value)
    {
        if (!_showing.IsActive && _steps.Count > 0)
        {
            ChangeStep(step => step with { FadeSeconds = Seconds(value) }, notifyColor: false);
        }
    }

    // The same message for another name is news too: a screen reader says it again.
    private void ShowNameError(string? error)
    {
        if (error is not null && error == NameError)
        {
            OnPropertyChanged(nameof(NameError));
        }

        NameError = error;
    }

    // Tenths of a second, within the range.
    private static double Seconds(double value) => Math.Round(Math.Clamp(value, CustomEffects.MinSeconds, CustomEffects.MaxSeconds), 1);

    private void MoveStep(int index)
    {
        (_steps[SelectedIndex], _steps[index]) = (_steps[index], _steps[SelectedIndex]);
        StepsChanged(index);
    }

    private void ChangeStep(Func<CustomEffectStep, CustomEffectStep> change, bool notifyColor)
    {
        _steps[SelectedIndex] = change(_steps[SelectedIndex]);
        Steps[SelectedIndex].Step = _steps[SelectedIndex];
        OnPropertyChanged(nameof(SelectedStepText));
        if (notifyColor)
        {
            OnPropertyChanged(nameof(SelectedColor));
        }

        OnChanged();
    }

    private void StepsChanged(int selected)
    {
        _showing.Run(() => Refresh(selected));

        // Another step now sits at the selected place, also when the index stayed the same.
        OnPropertyChanged(nameof(SelectedColor));
        OnPropertyChanged(nameof(SelectedStepText));
        OnChanged();
    }

    // Shows the steps and selects one, with its times. Changes only what differs, so the tiles that
    // stay keep their place and the keyboard focus.
    private void Refresh(int selected)
    {
        SelectedIndex = Math.Max(selected, 0);
        while (Steps.Count > _steps.Count)
        {
            Steps.RemoveAt(Steps.Count - 1);
        }

        for (var i = 0; i < _steps.Count; i++)
        {
            if (i < Steps.Count)
            {
                Steps[i].Step = _steps[i];
                Steps[i].IsSelected = i == SelectedIndex;
            }
            else
            {
                Steps.Add(new CustomEffectStepItem(i, _steps[i], i == SelectedIndex));
            }
        }

        if (_steps.Count > 0)
        {
            HoldSeconds = _steps[SelectedIndex].HoldSeconds;
            FadeSeconds = _steps[SelectedIndex].FadeSeconds;
        }

        OnPropertyChanged(nameof(SelectedColor));
        OnPropertyChanged(nameof(SelectedStepText));
        AddStepCommand.NotifyCanExecuteChanged();
        RemoveStepCommand.NotifyCanExecuteChanged();
        MoveStepEarlierCommand.NotifyCanExecuteChanged();
        MoveStepLaterCommand.NotifyCanExecuteChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, Effect);
}
