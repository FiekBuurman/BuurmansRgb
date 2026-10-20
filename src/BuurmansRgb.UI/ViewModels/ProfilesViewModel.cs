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

        SelectedName = Find(selected);
        OnPropertyChanged(nameof(SaveLabel));
    }

    partial void OnSelectedNameChanged(string? value)
    {
        if (value is not null)
        {
            Name = value;
        }
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
