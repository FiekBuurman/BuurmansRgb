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
    public partial IReadOnlyList<SceneDot> Dots { get; private set; } = SceneLook.Unknown;

    [ObservableProperty]
    public partial string Description { get; private set; } = string.Empty;

    // The scene that is on: the one loaded or saved last, until the lighting changed otherwise.
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public void Show(LightingInfo lighting, ProfileAutomation automation)
    {
        Dots = SceneLook.DotsOf(lighting);
        Description = SceneLook.Describe(lighting, automation);
    }
}
