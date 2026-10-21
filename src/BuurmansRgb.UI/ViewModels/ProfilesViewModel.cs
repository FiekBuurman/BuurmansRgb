using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The saved profiles, which the app calls scenes: a name to save the lighting under, and a list to
// load or delete one from, also as cards (Cards). This only asks; MainViewModel sends the request to
// the engine and shows the new list.
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    public event EventHandler<string>? SaveRequested;

    public event EventHandler<string>? LoadRequested;

    public event EventHandler<string>? DeleteRequested;

    // Raised with the selected profile when its automation is to be shown: when another profile is
    // selected. Not for the same profile again, so what the user typed there stays.
    public event EventHandler<string>? AutomationNeeded;

    // When the selected profile loads by itself.
    public ProfileAutomationViewModel Automation { get; } = new();

    // In the order of the engine, which is alphabetical.
    public ObservableCollection<string> Names { get; } = [];

    // The same profiles as cards on the Scènes page, in the same order.
    public ObservableCollection<SceneCard> Cards { get; } = [];

    public bool HasScenes => Names.Count > 0;

    // The scene that is on (ShowActive); null when none is.
    public string? ActiveName { get; private set; }

    // Picking a profile in the list fills in its name, so saving then updates that profile.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveLabel))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(EditTitle))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(DeleteCommand), nameof(OverwriteCommand))]
    public partial string? SelectedName { get; set; }

    public bool HasSelection => SelectedName is not null;

    // The heading of the settings of the chosen scene, such as "Klusmodus aanpassen".
    public string? EditTitle => SelectedName is { } name ? UiText.Format(Strings.SceneEditAccessibleName, name) : null;

    // The name field for a new scene shows, after "+ Nieuwe scène in elkaar zetten".
    [ObservableProperty]
    public partial bool IsNaming { get; private set; }

    // Says so when saving replaces a profile that exists.
    public string SaveLabel => Find(Name) is null ? Strings.ProfileSave : Strings.ProfileOverwrite;

    // Asked before a profile is deleted, with its name; the view shows the question. Without it,
    // a profile is deleted without asking.
    public Func<string, bool>? ConfirmDelete { get; set; }

    // Shows the names of the engine and selects select, or else the profile that was selected,
    // when it is still there.
    public void Show(IReadOnlyList<string> names, string? select = null)
    {
        // Clearing the list makes a ListBox select nothing for a moment; the automation only follows
        // where the selection ends up.
        var selected = select ?? SelectedName;
        _showing.Run(() =>
        {
            var cards = Cards.ToDictionary(card => card.Name);
            Names.Clear();
            Cards.Clear();
            foreach (var name in names)
            {
                Names.Add(name);
                var card = cards.GetValueOrDefault(name) ?? new SceneCard(name);
                card.IsActive = IsActiveName(name);
                Cards.Add(card);
            }

            SelectedName = Find(selected);
        });
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(HasScenes));
        RequestAutomation();
    }

    // What the engine says the scene looks like and when it loads by itself.
    public void ShowCard(string name, LightingInfo lighting, ProfileAutomation automation) =>
        Cards.FirstOrDefault(card => card.Name == name)?.Show(lighting, automation);

    // The scene that is on, as the engine says; null when none is. Its card gets the border of the
    // design.
    public void ShowActive(string? name)
    {
        ActiveName = name;
        foreach (var card in Cards)
        {
            card.IsActive = IsActiveName(card.Name);
        }
    }

    private bool IsActiveName(string name) => ActiveName is { } active && ProfileNames.Comparer.Equals(name, active);

    partial void OnSelectedNameChanged(string? value)
    {
        if (value is not null)
        {
            Name = value;
        }

        if (!_showing.IsActive)
        {
            RequestAutomation();
        }
    }

    // The fields of another profile are emptied right away, so they never show another profile's.
    private void RequestAutomation()
    {
        if (SelectedName is { } name && Automation.ProfileName is { } shown && ProfileNames.Comparer.Equals(name, shown))
        {
            return;
        }

        Automation.Clear();
        if (SelectedName is { } selected)
        {
            AutomationNeeded?.Invoke(this, selected);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        IsNaming = false;
        SaveRequested?.Invoke(this, ProfileNames.Normalize(Name)!);
    }

    private bool CanSave() => ProfileNames.Normalize(Name) is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Load() => LoadRequested?.Invoke(this, SelectedName!);

    // A click on a scene card: one click, and the whole house joins in.
    [RelayCommand]
    private void LoadScene(SceneCard card) => LoadRequested?.Invoke(this, card.Name);

    // Chooses the scene to overwrite, delete or load by itself.
    [RelayCommand]
    private void Edit(SceneCard card) => SelectedName = card.Name;

    // Saves the lighting as it is now under the chosen scene.
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Overwrite() => SaveRequested?.Invoke(this, SelectedName!);

    // An empty name, so saving makes a new scene instead of overwriting the chosen one.
    [RelayCommand]
    private void StartNew()
    {
        Name = string.Empty;
        IsNaming = true;
    }

    [RelayCommand]
    private void CancelNew() => IsNaming = false;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        var name = SelectedName!;
        if (ConfirmDelete?.Invoke(name) ?? true)
        {
            DeleteRequested?.Invoke(this, name);
        }
    }

    private string? Find(string? name) =>
        ProfileNames.Normalize(name) is { } normalized
            ? Names.FirstOrDefault(other => ProfileNames.Comparer.Equals(other, normalized))
            : null;
}
