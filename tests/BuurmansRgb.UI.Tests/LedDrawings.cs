using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// What a LedPreviewView drew, without the unseen area that lets the mouse find it near an LED
// (LedLayout.HitRadius): a drawing per LED, and a ring for the selection or the focus.
internal static class LedDrawings
{
    // What OnRender drew last, once WPF has laid out and drawn what was pending.
    public static List<GeometryDrawing> Of(Visual view)
    {
        OffscreenWindow.WaitUntilIdle();
        return [.. (VisualTreeHelper.GetDrawing(view)?.Children ?? []).Where(drawing => !IsHitArea(drawing)).Cast<GeometryDrawing>()];
    }

    // A fill without a color and without a pen, which only the hit area has.
    public static bool IsHitArea(Drawing drawing) =>
        drawing is GeometryDrawing { Pen: null, Brush: SolidColorBrush { Color.A: 0 } };
}
