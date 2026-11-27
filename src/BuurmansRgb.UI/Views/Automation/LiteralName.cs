using System.Windows;
using System.Windows.Automation;

namespace BuurmansRgb.UI.Views;

// The name set on a button, for its automation peer, as it is. The peer of a button of WPF with a text
// as its content (ButtonBaseAutomationPeer) takes the first "_" of its name for the mark of an access
// key and drops it, also from a name set on it: "Zone ARGB_V2_1 van ... laten knipperen" was read as
// "ARGBV2_1". The app has no access keys, so every "_" in a name is one of a name from OpenRGB or of
// the user, such as a zone, a device or an own effect, and stays.
internal static class LiteralName
{
    // The name set on owner, or null without one, so the peer falls back on its own.
    public static string? Of(UIElement owner) =>
        AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : null;
}
