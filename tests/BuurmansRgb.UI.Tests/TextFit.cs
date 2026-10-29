using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// What a larger text can do wrong in a window, as far as it shows: text that is cut off, by the room
// it gets or at the edge of an element around it, a field too narrow for its text, and text over
// another text or over a control. A
// name in a card title may end in "…" (CardTitle), as it does at 100 % with a long name.
internal static class TextFit
{
    public static List<string> Problems(FrameworkElement root)
    {
        var problems = new List<string>();
        var texts = new List<(TextBlock Text, Rect Ink)>();
        foreach (var text in Visible<TextBlock>(root).Where(text => text.Text.Length > 0))
        {
            if (CutOff(text) is { } cut)
            {
                problems.Add($"'{text.Text}' {cut}");
            }

            if (Shown(text, InkOf(text), root) is { } ink)
            {
                texts.Add((text, ink));
            }
        }

        // A field scrolls its text sideways when it is too narrow for it.
        foreach (var box in Visible<TextBox>(root).Where(box => box.Text.Length > 0))
        {
            if (box.ExtentWidth > box.ViewportWidth + 0.5 || box.HorizontalOffset > 0)
            {
                problems.Add(Invariant($"'{box.Text}' needs {box.ExtentWidth:0.#} px in a field with {box.ViewportWidth:0.#}"));
            }
            else if (CutOff(box) is { } cut)
            {
                problems.Add($"'{box.Text}' {cut}");
            }
        }

        for (var i = 0; i < texts.Count; i++)
        {
            for (var j = i + 1; j < texts.Count; j++)
            {
                if (Overlap(texts[i].Ink, texts[j].Ink))
                {
                    problems.Add($"'{texts[i].Text.Text}' lies over '{texts[j].Text.Text}'");
                }
            }
        }

        // Controls of the window itself, not the parts of one, such as the toggle button under the
        // text of a ComboBox.
        foreach (var control in Visible<Control>(root).Where(control => control is ButtonBase or TextBox or ComboBox or Slider && control.TemplatedParent is not Control))
        {
            if (Shown(control, new Rect(control.RenderSize), root) is not { } bounds)
            {
                continue;
            }

            foreach (var (text, ink) in texts)
            {
                if (!IsInside(text, control) && Overlap(bounds, ink))
                {
                    problems.Add($"'{text.Text}' lies over {control.GetType().Name} '{AutomationProperties.GetName(control)}'");
                }
            }
        }

        return problems;
    }

    // WPF arranges an element that needs more room than its slot whole, and cuts off what lies
    // outside the slot: the text itself or an element around it. Not a scroll viewer, which shows
    // the rest when it scrolls; but a page does not scroll sideways, so text past its right edge is
    // cut off too. With UseLayoutRounding at a scale of Windows such as 125 %, WPF can round a Border
    // with padding one pixel of the screen smaller than its content, at 100 % too; that pixel does not
    // count.
    private static string? CutOff(FrameworkElement text)
    {
        var pixel = (1 / VisualTreeHelper.GetDpi(text).DpiScaleY) + 0.01;
        for (DependencyObject? element = text; element is not null and not Window; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollContentPresenter presenter)
            {
                var viewer = (ScrollViewer)presenter.TemplatedParent;
                var right = text.TranslatePoint(new Point(text.ActualWidth, 0), presenter).X;
                if (viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled && right > presenter.ActualWidth + 0.5)
                {
                    return Invariant($"ends at {right:0.#}, past the {presenter.ActualWidth:0.#} of its scroll viewer");
                }

                continue;
            }

            if (element is FrameworkElement framework && LayoutInformation.GetLayoutClip(framework) is { } clip
                && (clip.Bounds.Right < framework.ActualWidth - pixel || clip.Bounds.Bottom < framework.ActualHeight - pixel))
            {
                return Invariant($"is cut off in {framework.GetType().Name} '{framework.Name}' of {framework.ActualWidth:0.#} × {framework.ActualHeight:0.#} at {clip.Bounds.Right:0.#} × {clip.Bounds.Bottom:0.#}");
            }
        }

        return null;
    }

    // Where the letters are in the text block, which may be wider than they: as wide as the text,
    // placed by its alignment.
    private static Rect InkOf(TextBlock text)
    {
        var whole = new TextBlock
        {
            Text = text.Text,
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontWeight = text.FontWeight,
            FontStyle = text.FontStyle,
            FontStretch = text.FontStretch,
            TextWrapping = text.TextWrapping,
            Padding = text.Padding,
        };
        whole.Measure(new Size(text.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : text.ActualWidth, double.PositiveInfinity));
        var width = Math.Min(whole.DesiredSize.Width, text.ActualWidth);
        var left = text.TextAlignment switch
        {
            TextAlignment.Center => (text.ActualWidth - width) / 2,
            TextAlignment.Right => text.ActualWidth - width,
            _ => 0,
        };
        return new Rect(left, 0, width, text.ActualHeight);
    }

    // The part of bounds, in the element, that shows in root: within every scroll viewer around it.
    private static Rect? Shown(FrameworkElement element, Rect bounds, FrameworkElement root)
    {
        var shown = element.TransformToAncestor(root).TransformBounds(bounds);
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null && parent != root; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollContentPresenter presenter)
            {
                shown.Intersect(presenter.TransformToAncestor(root).TransformBounds(new Rect(presenter.RenderSize)));
                if (shown.IsEmpty)
                {
                    return null;
                }
            }
        }

        return shown;
    }

    private static IEnumerable<T> Visible<T>(FrameworkElement root)
        where T : FrameworkElement =>
        AppTheme.Descendants<T>(root).Where(element => element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0 && element.Opacity > 0);

    // More than a hairline in common: rounding and the ascent of a font may touch.
    private static bool Overlap(Rect first, Rect second)
    {
        var common = Rect.Intersect(first, second);
        return !common.IsEmpty && common.Width > 1 && common.Height > 1;
    }

    private static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (var parent = element; parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
