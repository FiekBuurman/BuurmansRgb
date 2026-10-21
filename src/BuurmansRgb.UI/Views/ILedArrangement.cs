using System.Windows;

namespace BuurmansRgb.UI.Views;

// Where a LedPreviewView draws the LEDs of a zone at one width, from the top left of the view:
// the box of each LED, the size of the whole and the LED under a point. Measure, OnRender, the
// click, the tooltip and UI Automation all use the same arrangement, so a click lands on the LED
// that is drawn there.
internal interface ILedArrangement
{
    Size Size { get; }

    // How much smaller than full size the LEDs are, for a key map or ring that shrinks to fit.
    double Scale { get; }

    // Keys are drawn as rounded rectangles, other LEDs as dots.
    bool Keys { get; }

    // The box of each LED of the zone; Rect.Empty for an LED it does not show.
    IReadOnlyList<Rect> Bounds { get; }

    // NoLed for a gap or a point outside the LEDs.
    int LedAt(Point point);
}
