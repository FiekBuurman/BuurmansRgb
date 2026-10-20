namespace BuurmansRgb.UI.ViewModels;

// One page in the navigation rail. Label is its Dutch name; the settings sit at the bottom.
public sealed record NavigationItem(AppPage Page, string Label)
{
    public bool IsAtBottom => Page == AppPage.Settings;

    // A screen reader names a list item by its ToString.
    public override string ToString() => Label;
}
