using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// When the selected profile loads by itself: every day at a time, and while a program runs. An
// empty field leaves that way off. This only asks; MainViewModel sends the change to the engine
// and shows what the engine saved.
public sealed partial class ProfileAutomationViewModel : ObservableObject
{
    private const string TimeFormat = "HH:mm";

    // "H" also reads "07:30"; a dot is how many people write a time in Dutch.
    private static readonly string[] TimeFormats = ["H:mm", "H.mm"];

    // What the engine has, so Apply only runs for a change.
    private ProfileAutomation _saved = ProfileAutomation.None;

    public event EventHandler<ProfileAutomationChange>? ApplyRequested;

    // The profile it is about; null while no profile is selected.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string? ProfileName { get; private set; }

    public bool HasProfile => ProfileName is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string LoadAtText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string ProgramText { get; set; } = string.Empty;

    // Whether the saved profile loads by itself, for the heading that stays when folded up.
    public string Header => _saved == ProfileAutomation.None ? Strings.ProfileAutomationOff : Strings.ProfileAutomationOn;

    // What is wrong with the fields, or null.
    public string? Error =>
        !TryParseTime(LoadAtText, out _) ? Strings.ProfileLoadAtInvalid
        : ProgramNames.IsShortcutOrScript(ProgramText) ? Strings.ProfileProgramShortcut
        : !string.IsNullOrWhiteSpace(ProgramText) && ProgramNames.Normalize(ProgramText) is null ? Strings.ProfileProgramInvalid
        : null;

    public bool HasError => Error is not null;

    // Shows what the engine saved for the profile.
    public void Show(string profileName, ProfileAutomation automation) => Display(profileName, automation);

    public void Clear() => Display(null, ProfileAutomation.None);

    private void Display(string? profileName, ProfileAutomation automation)
    {
        _saved = automation;
        ProfileName = profileName;
        LoadAtText = automation.LoadAt?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? string.Empty;
        ProgramText = automation.Program ?? string.Empty;
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

        return new ProfileAutomation(loadAt, ProgramText).Normalize();
    }

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
