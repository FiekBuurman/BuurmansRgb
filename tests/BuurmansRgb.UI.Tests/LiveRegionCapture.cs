using System.Windows;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// What LiveRegion would give a screen reader that listens, collected instead of read out; once
// disposed, LiveRegion is as before, also when an assert failed.
internal sealed class LiveRegionCapture : IDisposable
{
    private readonly List<UIElement> announced = [];
    private readonly Func<bool> hasListener;
    private readonly Action<UIElement> announce;

    private LiveRegionCapture()
    {
        (hasListener, announce) = (LiveRegion.HasListener, LiveRegion.Announce);
        LiveRegion.HasListener = () => true;
        LiveRegion.Announce = announced.Add;
    }

    // The elements read out, in order.
    public IReadOnlyList<UIElement> Announced => announced;

    public static LiveRegionCapture Start() => new();

    public void Dispose() => (LiveRegion.HasListener, LiveRegion.Announce) = (hasListener, announce);
}
