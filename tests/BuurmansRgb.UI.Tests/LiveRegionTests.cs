using System.Windows.Controls;
using System.Windows.Data;
using BuurmansRgb.UI.Views;
using System.ComponentModel;

namespace BuurmansRgb.UI.Tests;

// With a real window off screen, as an element only loads in one.
public sealed class LiveRegionTests
{
    [Fact]
    public void TextOfAnElementBeingMade_IsNotReadOutButAChangeAfterwardsIs() => OffscreenWindow.Run(panel =>
    {
        using var live = LiveRegionCapture.Start();
        var status = new Status { Text = "OpenRGB: niet bereikbaar" };
        var first = Region(panel, status);
        OffscreenWindow.WaitUntilIdle();
        Assert.Empty(live.Announced);

        status.Text = "OpenRGB: verbonden, 3 apparaten";
        OffscreenWindow.WaitUntilIdle();
        Assert.Equal([first], live.Announced);

        // The page is made again when the user comes back to it.
        panel.Children.Remove(first);
        Region(panel, status);
        OffscreenWindow.WaitUntilIdle();
        Assert.Equal([first], live.Announced);
    });

    private static TextBlock Region(Panel panel, Status status)
    {
        var text = new TextBlock { DataContext = status };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Status.Text)) { NotifyOnTargetUpdated = true });
        LiveRegion.SetAnnouncesUpdates(text, true);
        panel.Children.Add(text);
        return text;
    }

    private sealed class Status : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Text
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        } = string.Empty;
    }
}
