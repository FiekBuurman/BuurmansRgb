using System.Globalization;

namespace BuurmansRgb.UI.ViewModels;

internal static class UiText
{
    // Fills in a text from Strings.resx in the culture of the user.
    public static string Format(string format, params ReadOnlySpan<object?> values) =>
        string.Format(CultureInfo.CurrentCulture, format, values);
}
