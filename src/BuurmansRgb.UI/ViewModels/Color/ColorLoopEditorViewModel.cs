using BuurmansRgb.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The colors of the color-loop effect, and whether they spread over the LEDs. LoopChanged is
// raised only for a change the user made, not for a loop shown with Show.
public sealed partial class ColorLoopEditorViewModel : ColorListEditorViewModel
{
    public ColorLoopEditorViewModel()
        : base(ColorLoop.MinColors, ColorLoop.MaxColors, ColorLoop.Default.Colors) =>
        ColorsChanged += (_, _) => LoopChanged?.Invoke(this, Loop);

    public event EventHandler<ColorLoop>? LoopChanged;

    [ObservableProperty]
    public partial bool Spread { get; set; } = true;

    public ColorLoop Loop => new(Colors, Spread);

    // Shows the loop of the engine; the selection stays on the same place when it still exists.
    public void Show(ColorLoop loop) => Showing.Run(() =>
    {
        Show(loop.Colors);
        Spread = loop.Spread;
    });

    partial void OnSpreadChanged(bool value)
    {
        if (!Showing.IsActive)
        {
            LoopChanged?.Invoke(this, Loop);
        }
    }
}
