using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// When the selected profile loads by itself: at a time on the chosen days, and while a program runs.
// An empty field leaves that way off. This only asks; MainViewModel sends the change to the engine
// and shows what the engine saved.
public sealed partial class ProfileAutomationViewModel : ObservableObject
{
    private const string TimeFormat = "HH:mm";

    // "H" also reads "07:30"; a dot is how many people write a time in Dutch.
    private static readonly string[] TimeFormats = ["H:mm", "H.mm"];

    private readonly Func<IReadOnlyList<string>> _findPrograms;

    // What the engine has, so Apply only runs for a change.
    private ProfileAutomation _saved = ProfileAutomation.None;

    // findPrograms lists the programs to pick from, as .exe names (WindowedPrograms.Find).
    public ProfileAutomationViewModel(Func<IReadOnlyList<string>>? findPrograms = null)
    {
        _findPrograms = findPrograms ?? WindowedPrograms.Find;
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => OnDaysChanged();
        }
    }

    // Monday to Sunday; all of them is every day. They count only with a time.
    public IReadOnlyList<DayOption> Days { get; } = DayOption.CreateWeek();

    public bool HasLoadAt => !string.IsNullOrWhiteSpace(LoadAtText);

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

    // The profile it is about; null while no profile is selected.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string? ProfileName { get; private set; }

    public bool HasProfile => ProfileName is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadAt))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string LoadAtText { get; set; } = string.Empty;

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

    partial void OnLoadAtTextChanged(string value) => Error = FindError();

    partial void OnProgramTextChanged(string value) => Error = FindError();

    private void OnDaysChanged()
    {
        Error = FindError();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private WeekDays CheckedDays() =>
        Days.Where(day => day.IsChecked).Aggregate(WeekDays.None, (days, day) => days | day.Day);

    // Shows what the engine saved for the profile.
    public void Show(string profileName, ProfileAutomation automation) => Display(profileName, automation);

    public void Clear() => Display(null, ProfileAutomation.None);

    private void Display(string? profileName, ProfileAutomation automation)
    {
        _saved = automation;
        ProfileName = profileName;
        LoadAtText = automation.LoadAt?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? string.Empty;
        ProgramText = automation.Program ?? string.Empty;
        foreach (var day in Days)
        {
            day.IsChecked = (automation.Days & day.Day) != WeekDays.None;
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
        if (HasError || !TryParseTime(LoadAtText, out var loadAt))
        {
            return null;
        }

        return new ProfileAutomation(loadAt, ProgramText, CheckedDays()).Normalize();
    }

    private string? FindError() =>
        !TryParseTime(LoadAtText, out _) ? Strings.ProfileLoadAtInvalid
        : HasLoadAt && CheckedDays() == WeekDays.None ? Strings.ProfileDaysNone
        : ProgramNames.IsShortcutOrScript(ProgramText) ? Strings.ProfileProgramShortcut
        : !string.IsNullOrWhiteSpace(ProgramText) && ProgramNames.Normalize(ProgramText) is null ? Strings.ProfileProgramInvalid
        : null;

    // An empty text is no time, which is valid: off.
    private static bool TryParseTime(string text, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!TimeOnly.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        time = parsed;
        return true;
    }
}
