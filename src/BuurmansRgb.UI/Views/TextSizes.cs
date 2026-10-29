using System.Windows;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Views;

// Follows the text size of Windows: the font sizes of Themes/Tokens.xaml for text take the factor of
// that setting, while the app runs too, and go back to those of the design at 100 %. It does so the
// way ContrastTheme does the colors, with a dictionary of its own on top of the theme (a
// ResourceOverlay), so the XAML has to refer to these sizes with DynamicResource. Icons, LEDs, click
// sizes and the word mark do not take the factor: the word mark is the logo, in a title bar of a
// fixed height, like the app icon beside it.
public sealed class TextSizes : IDisposable
{
    // The sizes of text in the tokens: every one but WordmarkSize.
    internal static readonly IReadOnlyList<string> FontSizeKeys =
    [
        "ScreenTitleSize", "BigValueSize", "CountValueSize", "SmallScreenTitleSize", "SectionHeaderSize", "CardTitleSize",
        "SceneCardTitleSize", "PrimaryButtonSize", "NewButtonSize", "BodySize", "SmallBodySize", "SubTextSize",
        "NavLabelSize", "HexCodeSize",
    ];

    private static readonly Uri Tokens = new("/BuurmansRgb.UI;component/Themes/Tokens.xaml", UriKind.Relative);

    // The sizes of the design, read once from the tokens rather than from the resources this follows
    // on, which may be those of a window, without the tokens.
    private static readonly Lazy<IReadOnlyDictionary<string, double>> Design = new(ReadDesignSizes);

    private readonly ResourceOverlay _sizes;
    private readonly ITextScale _textScale;

    private TextSizes(ResourceDictionary resources, ITextScale textScale)
    {
        _sizes = new ResourceOverlay(resources);
        _textScale = textScale;
    }

    // The factor the sizes in the resources have now: 1.0 while the sizes of the design are in them.
    public double Factor { get; private set; } = 1.0;

    internal static IReadOnlyDictionary<string, double> DesignSizes => Design.Value;

    // From now on, the resources of the app (or of a test) follow textScale.
    public static TextSizes Follow(ResourceDictionary resources, ITextScale textScale)
    {
        var sizes = new TextSizes(resources, textScale);
        sizes.Apply();
        textScale.Changed += sizes.OnChanged;
        return sizes;
    }

    // Stops following; the sizes stay as they are.
    public void Dispose() => _textScale.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        var factor = _textScale.Factor;
        if (factor == Factor)
        {
            return;
        }

        _sizes.Set(factor == 1.0 ? null : Scaled(factor));
        Factor = factor;
    }

    private static ResourceDictionary Scaled(double factor)
    {
        var sizes = new ResourceDictionary();
        foreach (var (key, size) in DesignSizes)
        {
            sizes[key] = size * factor;
        }

        return sizes;
    }

    private static IReadOnlyDictionary<string, double> ReadDesignSizes()
    {
        var tokens = (ResourceDictionary)Application.LoadComponent(Tokens);
        return FontSizeKeys.ToDictionary(key => key, key => (double)tokens[key]);
    }
}
