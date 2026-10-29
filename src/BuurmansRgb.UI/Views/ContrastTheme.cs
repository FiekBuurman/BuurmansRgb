using System.Windows;
using System.Windows.Media;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Views;

// Follows the contrast themes of Windows: while one is on, the tokens of Themes/Tokens.xaml for text,
// surfaces, borders, links, focus and selection take the colors of that theme (SystemColors), and
// when it goes off the colors of the design come back, both while the app runs. It does so with a
// dictionary of its own on top of the theme, so the XAML has to refer to these tokens with
// DynamicResource. The LEDs, their glow, the strip board under them and the colors of a scene are
// content and keep their colors, as do the logo and the R, G and B of the sliders on Kleur.
public sealed class ContrastTheme : IDisposable
{
    // Per token the color it takes in a contrast theme. Muted text takes the text color, not
    // GrayText: in a contrast theme that means disabled. Surfaces are all the window, so borders and
    // the outline around the track of a switch and a slider, in the text color, show where a card or
    // a control is (not GrayText, which would make a slider look disabled); a selected or hovered
    // item takes the highlight, as in Windows. Buttons and cards you click take the face and text of
    // a button (in WPF Control and ControlText); the accent (the primary button, a ticked check box,
    // a day that is on) takes the highlight, like a switch that is on. Every hover shows in the
    // highlight: a link also gets a line under it, a switch a ring in the highlight around its
    // track, and the primary button, already the highlight, swaps its colors and gets a ring.
    internal static readonly IReadOnlyDictionary<string, Func<SolidColorBrush>> SystemBrushes = new Dictionary<string, Func<SolidColorBrush>>
    {
        // Text, surfaces and borders.
        ["TextBrush"] = () => SystemColors.WindowTextBrush,
        ["TextMutedBrush"] = () => SystemColors.WindowTextBrush,
        ["BgBrush"] = () => SystemColors.WindowBrush,
        ["SurfaceBrush"] = () => SystemColors.WindowBrush,
        ["SurfaceRaisedBrush"] = () => SystemColors.WindowBrush,
        ["NavBgBrush"] = () => SystemColors.WindowBrush,
        ["PreviewBgBrush"] = () => SystemColors.WindowBrush,
        ["BorderBrush"] = () => SystemColors.WindowTextBrush,
        ["BorderStrongBrush"] = () => SystemColors.WindowTextBrush,
        ["InputBorderBrush"] = () => SystemColors.WindowTextBrush,
        ["EditingCardBorderBrush"] = () => SystemColors.HighlightBrush,
        ["WarningBrush"] = () => SystemColors.WindowTextBrush,

        // Hover, selection and links.
        ["HoverBorderBrush"] = () => SystemColors.HighlightBrush,
        ["ControlHoverBrush"] = () => SystemColors.HighlightBrush,
        ["HoverOutlineBrush"] = () => SystemColors.HighlightBrush,
        ["NavSelectedBrush"] = () => SystemColors.HighlightBrush,
        ["ItemHighlightBrush"] = () => SystemColors.HighlightBrush,
        ["CaptionPressedBrush"] = () => SystemColors.HighlightBrush,
        ["CloseHoverBrush"] = () => SystemColors.HighlightBrush,
        ["CloseHoverTextBrush"] = () => SystemColors.HighlightTextBrush,
        ["OnHighlightTextBrush"] = () => SystemColors.HighlightTextBrush,
        ["OnHighlightMutedTextBrush"] = () => SystemColors.HighlightTextBrush,
        ["TextSelectionBrush"] = () => SystemColors.HighlightBrush,
        ["LinkBrush"] = () => SystemColors.HotTrackBrush,
        ["LinkHoverBrush"] = () => SystemColors.HighlightBrush,

        // Buttons, the accent and the track of a switch or slider.
        ["ButtonFaceBrush"] = () => SystemColors.ControlBrush,
        ["ButtonTextBrush"] = () => SystemColors.ControlTextBrush,
        ["ButtonBorderBrush"] = () => SystemColors.ControlTextBrush,
        ["AccentBrush"] = () => SystemColors.HighlightBrush,
        ["OnAccentBrush"] = () => SystemColors.HighlightTextBrush,
        ["AccentHoverBrush"] = () => SystemColors.HighlightTextBrush,
        ["OnAccentHoverBrush"] = () => SystemColors.HighlightBrush,
        ["ControlTrackBrush"] = () => SystemColors.WindowBrush,
        ["ControlOutlineBrush"] = () => SystemColors.WindowTextBrush,
        ["SwitchOnCoverBrush"] = () => SystemColors.HighlightBrush,
        ["KnobOffBrush"] = () => SystemColors.WindowTextBrush,

        // The ring of an LED that is off or black, in the text color like the outline of a switch
        // that is off: it shows where an LED is on the window, which the preview and the card of
        // Hele huis become, and on the face of a button, which the cards of a device and a scene
        // (CardButton) become. The LED itself keeps its colors.
        ["LedOffRingBrush"] = () => SystemColors.WindowTextBrush,
        ["CardLedOffRingBrush"] = () => SystemColors.ControlTextBrush,
    };

    private readonly ResourceDictionary _resources;
    private readonly IHighContrast _highContrast;
    private ResourceDictionary? _colors;

    private ContrastTheme(ResourceDictionary resources, IHighContrast highContrast)
    {
        _resources = resources;
        _highContrast = highContrast;
    }

    // Whether the colors of a contrast theme are in the resources now.
    public bool IsApplied => _colors is not null;

    // From now on, the resources of the app (or of a test) follow highContrast.
    public static ContrastTheme Follow(ResourceDictionary resources, IHighContrast highContrast)
    {
        var theme = new ContrastTheme(resources, highContrast);
        theme.Apply();
        highContrast.Changed += theme.OnChanged;
        return theme;
    }

    // Stops following; the colors stay as they are.
    public void Dispose() => _highContrast.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => Apply();

    // Always anew while a contrast theme is on, as another one may have taken over or its colors may
    // have changed: SystemColors then has new brushes. A replace is one change of the resources, so
    // WPF looks the tokens up again once.
    private void Apply()
    {
        var merged = _resources.MergedDictionaries;
        var colors = _highContrast.IsOn ? SystemColorsDictionary() : null;
        var index = _colors is null ? -1 : merged.IndexOf(_colors);
        if (index >= 0 && colors is not null)
        {
            merged[index] = colors;
        }
        else
        {
            if (index >= 0)
            {
                merged.RemoveAt(index);
            }

            if (colors is not null)
            {
                merged.Add(colors);
            }
        }

        _colors = colors;
    }

    private static ResourceDictionary SystemColorsDictionary()
    {
        var colors = new ResourceDictionary();
        foreach (var (key, brush) in SystemBrushes)
        {
            colors[key] = brush();
        }

        return colors;
    }
}
