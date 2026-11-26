using System.Globalization;

namespace BuurmansRgb.Tests;

// Sets the language (CurrentUICulture, the one the resource files follow) of the running test for
// as long as it lives, and puts the old one back on Dispose. For a test that must not depend on
// the language of the pc or CI runner it runs on.
internal sealed class UseUICulture : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

    public UseUICulture(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentUICulture = _previous;
}
