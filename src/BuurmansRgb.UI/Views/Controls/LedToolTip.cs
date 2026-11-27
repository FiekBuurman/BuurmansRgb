using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The tooltip of the LED the mouse points at in a LedPreviewView: the one a click there picks,
// under the mouse or the nearest within LedLayout.HitRadius. A WPF tooltip belongs to a whole
// element and keeps its text while the mouse stays on it; this one follows the mouse from LED to
// LED, shows below the LED it names and stays away from the room far from every LED. Its timer
// only runs while the mouse waits on an LED.
internal sealed class LedToolTip
{
    private readonly ToolTip _toolTip;
    private readonly DispatcherTimer _delay;
    private int _led = LedMatrix.NoLed;

    public LedToolTip(FrameworkElement owner)
    {
        _toolTip = new ToolTip { PlacementTarget = owner, Placement = PlacementMode.Bottom };
        _delay = new DispatcherTimer(DispatcherPriority.Normal, owner.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(ToolTipService.GetInitialShowDelay(owner)),
        };
        _delay.Tick += (_, _) =>
        {
            _delay.Stop();
            _toolTip.IsOpen = true;
        };
    }

    // The LED it names, also while it waits for its delay; LedMatrix.NoLed while hidden.
    public int Led => _led;

    // The text of the LED at bounds (in the coordinates of the owner): at once when the tooltip of
    // another LED was open, otherwise after the usual delay. Without a text, as on a gap, it hides.
    public void Show(int led, string? text, Rect bounds)
    {
        if (led == _led)
        {
            return;
        }

        var wasOpen = _toolTip.IsOpen;
        Hide();
        if (text is null)
        {
            return;
        }

        _led = led;
        _toolTip.Content = text;
        _toolTip.PlacementRectangle = bounds;
        if (wasOpen)
        {
            _toolTip.IsOpen = true;
        }
        else
        {
            _delay.Start();
        }
    }

    public void Hide()
    {
        _delay.Stop();
        _toolTip.IsOpen = false;
        _led = LedMatrix.NoLed;
    }
}
