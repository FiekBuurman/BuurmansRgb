using System.Globalization;

namespace BuurmansRgb.Tests;

// Sets the region format (CurrentCulture) of the running test for as long as it lives, and puts
// the old one back on Dispose. For a test that must not depend on the language of the pc or CI
// runner it runs on.
internal sealed class UseCulture : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public UseCulture(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
