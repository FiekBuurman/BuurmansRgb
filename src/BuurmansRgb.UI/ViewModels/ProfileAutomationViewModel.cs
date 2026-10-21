using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// When the selected profile loads by itself: at times, each on its own days, and while a program
// runs. An empty field leaves that way off. This only asks; MainViewModel sends the change to the
// engine and shows what the engine saved.
public sealed partial class ProfileAutomationViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<string>> _findPrograms;

    // What the engine has, so Apply only runs for a change.
    private ProfileAutomation _saved = ProfileAutomation.None;

    // findPrograms lists the programs to pick from, as .exe names (WindowedPrograms.Find).
    public ProfileAutomationViewModel(Func<IReadOnlyList<string>>? findPrograms = null)
    {
        _findPrograms = findPrograms ?? WindowedPrograms.Find;
        Times.CollectionChanged += OnTimesChanged;
        Times.Add(new LoadTimeRow());
    }

    // The times, as the engine saved them from early to late, and the ones added since. Always at
    // least one row, empty when the profile has no time, so there is a field to type it in.
    public ObservableCollection<LoadTimeRow> Times { get; } = [];

    public event EventHandler<ProfileAutomationChange>? ApplyRequested;

    // The programs with a window, read again each time the list opens; empty until then.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningPrograms))]
    public partial IReadOnlyList<string> RunningPrograms { get; private set; } = [];

    public bool HasRunningPrograms => RunningPrograms.Count > 0;

    [RelayCommand]
    private void FindPrograms() => RunningPrograms = _findPrograms();

    // From the list, or from "Bladeren": an .exe anywhere, of which only the file name counts.
    [RelayCommand]
    private void PickProgram(string? program)
    {
        if (ProgramNames.Normalize(program) is { } name)
        {
            ProgramText = name;
        }
    }

    // An empty row at the end, on every day, up to ProfileAutomation.MaxTimes.
    [RelayCommand(CanExecute = nameof(CanAddTime))]
    private void AddTime() => Times.Add(new LoadTimeRow());

    private bool CanAddTime() => ProfileName is not null && Times.Count < ProfileAutomation.MaxTimes;

    // The only row is emptied instead, so the field stays.
    [RelayCommand(CanExecute = nameof(CanRemoveTime))]
    private void RemoveTime(LoadTimeRow? row)
    {
        if (row is null || !Times.Contains(row))
        {
            return;
        }

        if (Times.Count > 1)
        {
            Times.Remove(row);
        }
        else
        {
            Times[0] = new LoadTimeRow();
        }
    }

    private bool CanRemoveTime(LoadTimeRow? row) => row is not null && (Times.Count > 1 || row.HasTime);

    // The profile it is about; null while no profile is selected.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(AddTimeCommand))]
    public partial string? ProfileName { get; private set; }

    public bool HasProfile => ProfileName is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string ProgramText { get; set; } = string.Empty;

    // Whether the saved profile loads by itself, for the heading that stays when folded up; without
    // a profile there is nothing to tell.
    public string Header =>
        ProfileName is null ? Strings.ProfileAutomationHeader
        : _saved == ProfileAutomation.None ? Strings.ProfileAutomationOff
        : Strings.ProfileAutomationOn;

    // What is wrong with the fields, or null. Only raised when it really changes, so a screen
    // reader hears it once and not on every key.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    partial void OnProgramTextChanged(string value) => Error = FindError();

    // Clearing gives no old rows; Display lets go of them first.
    private void OnTimesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in e.OldItems?.OfType<LoadTimeRow>() ?? [])
        {
            row.Changed -= OnTimeChanged;
        }

        foreach (var row in e.NewItems?.OfType<LoadTimeRow>() ?? [])
        {
            row.Changed += OnTimeChanged;
        }

        for (var i = 0; i < Times.Count; i++)
        {
            Times[i].Number = i + 1;
        }

        OnTimeChanged(this, EventArgs.Empty);
        AddTimeCommand.NotifyCanExecuteChanged();
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        Error = FindError();
        ApplyCommand.NotifyCanExecuteChanged();
        RemoveTimeCommand.NotifyCanExecuteChanged();
    }

    // Shows what the engine saved for the profile.
    public void Show(string profileName, ProfileAutomation automation) => Display(profileName, automation);

    public void Clear() => Display(null, ProfileAutomation.None);

    private void Display(string? profileName, ProfileAutomation automation)
    {
        _saved = automation;
        ProfileName = profileName;
        ProgramText = automation.Program ?? string.Empty;
        foreach (var row in Times)
        {
            row.Changed -= OnTimeChanged;
        }

        Times.Clear();
        foreach (var time in automation.Times.DefaultIfEmpty())
        {
            Times.Add(new LoadTimeRow(time));
        }

        OnPropertyChanged(nameof(Header));
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => ApplyRequested?.Invoke(this, new ProfileAutomationChange(ProfileName!, Read()!));

    private bool CanApply() => ProfileName is not null && Read() is { } automation && automation != _saved;

    // The fields as the engine saves them, or null when one is not valid.
    private ProfileAutomation? Read()
    {
        if (HasError)
        {
            return null;
        }

        var times = new List<LoadTime>();
        foreach (var row in Times)
        {
            if (!row.TryRead(out var time))
            {
                return null;
            }

            if (time is not null)
            {
                times.Add(time);
            }
        }

        return new ProfileAutomation(times, ProgramText).Normalize();
    }

    private string? FindError() =>
        Times.Any(row => !row.TryRead(out _)) ? Strings.ProfileLoadAtInvalid
        : Times.Any(row => row.HasTime && row.CheckedDays == WeekDays.None) ? Strings.ProfileDaysNone
        : ProgramNames.IsShortcutOrScript(ProgramText) ? Strings.ProfileProgramShortcut
        : !string.IsNullOrWhiteSpace(ProgramText) && ProgramNames.Normalize(ProgramText) is null ? Strings.ProfileProgramInvalid
        : null;
}
