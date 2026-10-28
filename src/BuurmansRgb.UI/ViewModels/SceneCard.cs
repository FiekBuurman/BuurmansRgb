using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// A saved profile as a card on the Scènes page. The bulbs and the description come once the engine
// has told what the scene looks like (Show).
public sealed partial class SceneCard(string name) : ObservableObject
{
    public string Name { get; } = name;

    public string LoadAccessibleName => UiText.Format(Strings.SceneLoadAccessibleName, Name);

    public string EditAccessibleName => UiText.Format(Strings.SceneEditAccessibleName, Name);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DotsAccessibleName))]
    public partial IReadOnlyList<SceneDot> Dots { get; private set; } = SceneLook.Off;

    // Whether the engine has told what the scene looks like; until then the bulbs are dark.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DotsAccessibleName))]
    public partial bool IsShown { get; private set; }

    // The bulbs in one go for a screen reader, each color once: "Aan: #FF0000, #FFFF00", or "Uit".
    // No name while the engine has not told what the scene looks like (or could not), so a screen
    // reader skips the dark bulbs instead of calling the scene off.
    public string DotsAccessibleName =>
        !IsShown ? string.Empty
        : Dots.Any(dot => dot.IsLit)
            ? UiText.Format(Strings.SceneDotsOn, string.Join(", ", Dots.Where(dot => dot.IsLit).Select(dot => dot.Color.ToHex()).Distinct()))
            : Strings.SceneDotsOff;

    [ObservableProperty]
    public partial string Description { get; private set; } = string.Empty;

    // The scene that is on: the one loaded or saved last, until the lighting changed otherwise.
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public void Show(LightingInfo lighting, ProfileAutomation automation)
    {
        Dots = SceneLook.DotsOf(lighting);
        Description = SceneLook.Describe(lighting, automation);
        IsShown = true;
    }

    // WPF names the list item, the card, after it for screen readers.
    public override string ToString() => Name;
}
