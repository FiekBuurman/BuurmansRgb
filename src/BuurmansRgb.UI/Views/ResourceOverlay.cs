using System.Windows;

namespace BuurmansRgb.UI.Views;

// A dictionary of its own over the resources of the app (or of a test): it goes at the end of their
// merged dictionaries, so its keys win over those of the theme. Set puts another one in its place,
// in one change of the resources, so WPF looks the keys up again once; null takes it away. Two
// overlays on the same resources, such as those of ContrastTheme and TextScale, each keep to their
// own dictionary, whichever came first.
internal sealed class ResourceOverlay(ResourceDictionary resources)
{
    // The dictionary in the resources now, or null without one.
    public ResourceDictionary? Current { get; private set; }

    public void Set(ResourceDictionary? next)
    {
        var merged = resources.MergedDictionaries;
        var index = Current is null ? -1 : merged.IndexOf(Current);
        if (index >= 0 && next is not null)
        {
            merged[index] = next;
        }
        else
        {
            if (index >= 0)
            {
                merged.RemoveAt(index);
            }

            if (next is not null)
            {
                merged.Add(next);
            }
        }

        Current = next;
    }
}
