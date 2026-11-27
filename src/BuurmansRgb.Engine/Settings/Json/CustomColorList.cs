using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

// The own colors of the toolbox on Kleur ("Eigen kleuren"). The engine does nothing with them but
// keep them: the settings app stops when it closes, so it keeps them here, in settings.json.
internal sealed class CustomColorList
{
    private readonly Lock _gate = new();
    private IReadOnlyList<RgbColor> _colors = [];

    // Raised when Colors got other colors, on the thread that set them; read Colors for the newest.
    public event EventHandler? Changed;

    // For the settings app, which gets an error for one too many; settings.json and Colors leave
    // the rest out instead. Static, so the engine checks also without a list (in tests). The
    // parameter name is that of the IPC call (IEngineApi.SetCustomColorsAsync).
    public static void ThrowIfTooMany(IReadOnlyList<RgbColor> colors)
    {
        if (colors.Count > CustomColors.MaxColors)
        {
            throw new ArgumentException($"At most {CustomColors.MaxColors} own colors, not {colors.Count}.", nameof(colors));
        }
    }

    // Empty until settings.json or the settings app gives colors; kept as CustomColors.Normalize
    // gives them, so a color twice or one too many is left out.
    public IReadOnlyList<RgbColor> Colors
    {
        get
        {
            lock (_gate)
            {
                return _colors;
            }
        }
        set
        {
            var colors = CustomColors.Normalize(value);
            bool changed;
            lock (_gate)
            {
                changed = !_colors.SequenceEqual(colors);
                _colors = colors;
            }

            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
