using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// When the selected profile loads by itself: at times, each on its own days, and while one of its
// programs runs, or only while it is in the foreground. The programs are one field with a comma
// between the names ("valorant.exe, cs2.exe"), so one program is typed as before. An empty field
// leaves that way off. A name with a comma cannot be in the field. This only asks; MainViewModel
// sends the change to the engine and shows what the engine saved.
public sealed partial class ProfileAutomationViewModel : ObservableObject
{
    // Between the names in the field.
    private const char ProgramSeparator = ',';

    private readonly Func<Task<IReadOnlyList<RunningProgram>>> _findPrograms;
    private readonly IFileDialogs _files;

    // What the engine has, so Apply only runs for a change.
    private ProfileAutomation _saved = ProfileAutomation.None;

    // While Display fills in the fields.
    private readonly ReentrancyGuard _displaying = new();

    // Counts the times the programs are read, so only the newest list shows.
    private int _programSearches;

    // findPrograms lists the programs to pick from; by default RunningPrograms.Find on the thread
    // pool, as it reads the exe file of each program, which would make the list hang as it opens.
    // fileDialogs opens the dialog of "Bladeren…"; without it (NoDialogs) nothing is picked.
    public ProfileAutomationViewModel(Func<Task<IReadOnlyList<RunningProgram>>>? findPrograms = null, IFileDialogs? fileDialogs = null)
    {
        _findPrograms = findPrograms ?? (() => Task.Run(Services.RunningPrograms.Find));
        _files = fileDialogs ?? NoDialogs.Instance;
        Times.CollectionChanged += OnTimesChanged;
        Times.Add(new LoadTimeRow());
    }

    // The times, as the engine saved them from early to late, and the ones added since. Always at
    // least one row, empty when the profile has no time, so there is a field to type it in.
    public ObservableCollection<LoadTimeRow> Times { get; } = [];

    public event EventHandler<ProfileAutomationChange>? ApplyRequested;

    // The programs with a window, read when the list shows and again each time it opens; empty
    // until then. The list opens with what it had and gets the new programs a moment later.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningPrograms))]
    public partial IReadOnlyList<RunningProgramItem> RunningPrograms { get; private set; } = [];

    public bool HasRunningPrograms => RunningPrograms.Count > 0;

    // A list that opens again while the last one is still read gets the newest programs, also when
    // the reading of before ends later. The same programs as before leave the list as it is, so an
    // open list keeps its highlighted item and focus, and a screen reader hears no new list.
    [RelayCommand]
    private async Task FindProgramsAsync()
    {
        var search = ++_programSearches;
        var programs = await _findPrograms();
        if (search == _programSearches && !programs.SequenceEqual(RunningPrograms.Select(item => item.Program)))
        {
            RunningPrograms = [.. programs.Select(program => new RunningProgramItem(program))];
        }
    }

    // From "Kies uit wat nu draait": the field gets the exe name, not the name the list shows.
    [RelayCommand]
    private void PickRunningProgram(RunningProgramItem? item) => PickProgram(item?.Program.ExeName);

    // "Bladeren…": the program the user chose in the dialog, if any.
    [RelayCommand]
    private void BrowseProgram()
    {
        if (_files.ChooseProgram() is { } path)
        {
            PickProgram(path);
        }
    }

    // From the list, or from "Bladeren": an .exe anywhere, of which only the file name counts. It
    // comes after the names in the field, unless it is there already. A name with a comma would
    // fall apart in two names in the field, so it is refused with an error until the next change.
    // No XAML binds PickProgramCommand (the button uses BrowseProgramCommand); the generated command
    // is there for the tests.
    [RelayCommand]
    private void PickProgram(string? program)
    {
        if (ProgramNames.Normalize(program) is not { } name)
        {
            return;
        }

        if (name.Contains(ProgramSeparator))
        {
            Error = UiText.Format(Strings.ProfileProgramCommaPicked, name);
            return;
        }

        var names = SplitPrograms(ProgramText);
        if (!names.Any(other => ProgramNames.Comparer.Equals(ProgramNames.Normalize(other) ?? other, name)))
        {
            ProgramText = string.Join(ProgramSeparator + " ", names.Append(name));
        }
        else
        {
            Error = FindError();
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
    [NotifyPropertyChangedFor(nameof(HasProgramText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string ProgramText { get; set; } = string.Empty;

    // The choice only counts with a program, so it is grey without one; it stays as it is, so
    // emptying the field and typing again keeps it.
    public bool HasProgramText => SplitPrograms(ProgramText).Count > 0;

    // Whether the programs count only while one is in the foreground, instead of all the time they
    // run; one choice for all of them.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool ForegroundOnly { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    partial void OnProgramTextChanged(string value)
    {
        if (!_displaying.IsActive)
        {
            Error = FindError();
        }
    }

    // Also takes away the error of a refused pick, like any other change does.
    partial void OnForegroundOnlyChanged(bool value)
    {
        if (!_displaying.IsActive)
        {
            Error = FindError();
        }
    }

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
        if (_displaying.IsActive)
        {
            return;
        }

        Error = FindError();
        ApplyCommand.NotifyCanExecuteChanged();
        RemoveTimeCommand.NotifyCanExecuteChanged();
    }

    // Shows what the engine saved for the profile.
    public void Show(string profileName, ProfileAutomation automation) => Display(profileName, automation);

    public void Clear() => Display(null, ProfileAutomation.None);

    // The rows there are show the saved times and only the difference is added or taken away, so
    // the field that has the focus keeps it, also after "Toepassen" with Enter. The error is worked
    // out once at the end, not for each field on the way.
    private void Display(string? profileName, ProfileAutomation automation)
    {
        _displaying.Run(() =>
        {
            _saved = automation;
            ProfileName = profileName;
            ProgramText = string.Join(ProgramSeparator + " ", automation.Programs);
            ForegroundOnly = automation.ForegroundOnly;
            LoadTime?[] times = [.. automation.Times.DefaultIfEmpty()];
            while (Times.Count > times.Length)
            {
                Times.RemoveAt(Times.Count - 1);
            }

            for (var i = 0; i < times.Length; i++)
            {
                if (i < Times.Count)
                {
                    Times[i].Show(times[i]);
                }
                else
                {
                    Times.Add(new LoadTimeRow(times[i]));
                }
            }
        });

        OnTimeChanged(this, EventArgs.Empty);
        OnPropertyChanged(nameof(Header));
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

        return new ProfileAutomation(times, SplitPrograms(ProgramText), ForegroundOnly).Normalize();
    }

    // The names in the field, without the empty ones between two commas.
    private static List<string> SplitPrograms(string text) =>
        [.. text.Split(ProgramSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

    // With more times it says which one, the first that is wrong, by the number a screen reader
    // gives its field; with one time a number only gets in the way. The same for the programs, by
    // the name that is wrong; the last one only by its name once a comma comes after it, so the
    // error does not change on each key while that name is typed.
    private string? FindError() =>
        Times.FirstOrDefault(row => !row.TryRead(out _)) is { } invalid ? ForRow(invalid, Strings.ProfileLoadAtInvalid, Strings.ProfileTimeInvalid)
        : Times.FirstOrDefault(row => row.HasTime && row.CheckedDays == WeekDays.None) is { } dayless ? ForRow(dayless, Strings.ProfileDaysNone, Strings.ProfileTimeDaysNone)
        : FindProgramError(SplitPrograms(ProgramText));

    // A name with a comma, which an earlier version let a scene have ("a,b.exe"), shows as it was
    // saved, but would come back as two names; as long as it is in the field, nothing is applied.
    private string? FindProgramError(List<string> programs) =>
        _saved.Programs.FirstOrDefault(program => program.Contains(ProgramSeparator) && HasPiece(ProgramText, program)) is { } withComma ? UiText.Format(Strings.ProfileProgramCommaSaved, withComma)
        : programs.FirstOrDefault(ProgramNames.IsShortcutOrScript) is { } shortcut ? ForProgram(programs, shortcut, Strings.ProfileProgramShortcut, Strings.ProfileProgramShortcutNamed, Strings.ProfileProgramShortcutLast)
        : programs.FirstOrDefault(program => ProgramNames.Normalize(program) is null) is { } wrong ? ForProgram(programs, wrong, Strings.ProfileProgramInvalid, Strings.ProfileProgramInvalidNamed, Strings.ProfileProgramInvalidLast)
        : programs.Select(program => ProgramNames.Normalize(program)!).Distinct(ProgramNames.Comparer).Count() > ProfileAutomation.MaxPrograms ? UiText.Format(Strings.ProfileProgramsTooMany, ProfileAutomation.MaxPrograms)
        : null;

    // Whether the name is a piece of its own in the field: at the start or after a comma, and at the
    // end or before a comma, with spaces around it; "xa,b.exe" does not hold "a,b.exe".
    private static bool HasPiece(string text, string name)
    {
        for (var start = text.IndexOf(name, StringComparison.OrdinalIgnoreCase); start >= 0; start = text.IndexOf(name, start + 1, StringComparison.OrdinalIgnoreCase))
        {
            var before = text.AsSpan(0, start).TrimEnd();
            var after = text.AsSpan(start + name.Length).TrimStart();
            if ((before.IsEmpty || before[^1] == ProgramSeparator) && (after.IsEmpty || after[0] == ProgramSeparator))
            {
                return true;
            }
        }

        return false;
    }

    private string ForRow(LoadTimeRow row, string single, string numbered) =>
        Times.Count == 1 ? single : UiText.Format(numbered, row.Number);

    private string ForProgram(List<string> programs, string program, string single, string named, string last) =>
        programs.Count == 1 ? single
        : programs.IndexOf(program) == programs.Count - 1 && !ProgramText.TrimEnd().EndsWith(ProgramSeparator) ? last
        : UiText.Format(named, program);
}
