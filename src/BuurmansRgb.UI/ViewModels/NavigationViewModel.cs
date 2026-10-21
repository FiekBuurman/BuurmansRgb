using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Which page the window shows. The rail is a list, so Ctrl+click can deselect its item: then the page
// stays, and the item is selected again once WPF is done (it ignores a change to the source it is
// updating).
public sealed partial class NavigationViewModel : ObservableObject
{
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    public NavigationViewModel() => SelectedItem = Items[0];

    public static IReadOnlyList<NavigationItem> Items { get; } =
    [
        new(AppPage.Lamps, Strings.NavLamps),
        new(AppPage.Color, Strings.NavColor),
        new(AppPage.Scenes, Strings.NavScenes),
        new(AppPage.Settings, Strings.NavSettings),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    public partial NavigationItem? SelectedItem { get; set; }

    public AppPage CurrentPage { get; private set; }

    [RelayCommand]
    public void GoTo(AppPage page) => SelectedItem = Items.First(item => item.Page == page);

    partial void OnSelectedItemChanged(NavigationItem? oldValue, NavigationItem? newValue)
    {
        if (newValue is not null)
        {
            CurrentPage = newValue.Page;
            return;
        }

        if (oldValue is null)
        {
            return;
        }

        if (_uiContext is null)
        {
            SelectedItem = oldValue;
        }
        else
        {
            _uiContext.Post(_ => SelectedItem ??= oldValue, state: null);
        }
    }
}
