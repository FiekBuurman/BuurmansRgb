using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The saved profiles: a name to save the lighting under, and a list to load or delete one from.
// This only asks; MainViewModel sends the request to the engine and shows the new list.
public sealed partial class ProfilesViewModel : ObservableObject
{
    public event EventHandler<string>? SaveRequested;

    public event EventHandler<string>? LoadRequested;

    public event EventHandler<string>? DeleteRequested;

    // Raised with the selected profile when its automation is to be shown: when the user picks it,
    // and after every change, so the fields show what the engine saved.
    public event EventHandler<string>? AutomationNeeded;

    // When the selected profile loads by itself.
    public ProfileAutomationViewModel Automation { get; } = new();

    // In the order of the engine, which is alphabetical.
    public ObservableCollection<string> Names { get; } = [];

    // Picking a profile in the list fills in its name, so saving then updates that profile.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveLabel))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(DeleteCommand))]
    public partial string? SelectedName { get; set; }

    // Says so when saving replaces a profile that exists.
    public string SaveLabel => Find(Name) is null ? Strings.ProfileSave : Strings.ProfileOverwrite;

    // Asked before a profile is deleted, with its name; the view shows the question. Without it,
    // a profile is deleted without asking.
    public Func<string, bool>? ConfirmDelete { get; set; }

    // Shows the names of the engine and selects select, or else the profile that was selected,
    // when it is still there.
    public void Show(IReadOnlyList<string> names, string? select = null)
    {
        var selected = select ?? SelectedName;
        Names.Clear();
        foreach (var name in names)
        {
            Names.Add(name);
        }

        var before = SelectedName;
        SelectedName = Find(selected);
        OnPropertyChanged(nameof(SaveLabel));
        if (SelectedName == before)
        {
            RequestAutomation();
        }
    }

    partial void OnSelectedNameChanged(string? value)
    {
        if (value is not null)
        {
            Name = value;
        }

        RequestAutomation();
    }

    // The fields of another profile are emptied right away, so they never show another profile's.
    private void RequestAutomation()
    {
        if (SelectedName is not { } name)
        {
            Automation.Clear();
            return;
        }

        if (Automation.ProfileName != name)
        {
            Automation.Clear();
        }

        AutomationNeeded?.Invoke(this, name);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => SaveRequested?.Invoke(this, ProfileNames.Normalize(Name)!);

    private bool CanSave() => ProfileNames.Normalize(Name) is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Load() => LoadRequested?.Invoke(this, SelectedName!);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        var name = SelectedName!;
        if (ConfirmDelete?.Invoke(name) ?? true)
        {
            DeleteRequested?.Invoke(this, name);
        }
    }

    private bool HasSelection() => SelectedName is not null;

    private string? Find(string? name) =>
        ProfileNames.Normalize(name) is { } normalized
            ? Names.FirstOrDefault(other => ProfileNames.Comparer.Equals(other, normalized))
            : null;
}
