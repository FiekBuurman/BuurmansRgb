using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The slider with its name and value (ValueSlider), on its own in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class ValueSliderViewTests
{
    // The slider carries the name, help and status; the control adds no element of its own.
    [Fact]
    public void Slider_TakesTheNameHelpAndStatusOfTheControl()
    {
        AppTheme.Run(panel =>
        {
            var control = new ValueSlider { Name = "SpeedSlider", Label = "Snelheid", ValueText = "80%", HelpText = "Hulp", ItemStatus = "Snel" };
            panel.Children.Add(control);
            OffscreenWindow.WaitUntilIdle();
            var peer = UIElementAutomationPeer.CreatePeerForElement(control.Slider);

            Assert.Equal("Snelheid", peer.GetName());
            Assert.Equal("Hulp", peer.GetHelpText());
            Assert.Equal("Snel", peer.GetItemStatus());
            Assert.Equal("SpeedSlider", peer.GetAutomationId());
            Assert.Equal("Hulp", control.Slider.ToolTip);
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(control));
            Assert.Equal(["80%", "Snelheid"], AppTheme.Descendants<TextBlock>(control).Select(text => text.Text));
        });
    }

    // Without a name or help, the slider has no AutomationId, tooltip or help of its own.
    [Fact]
    public void Slider_WithoutNameOrHelp_HasNoAutomationIdOrToolTip()
    {
        AppTheme.Run(panel =>
        {
            var control = new ValueSlider { Label = "Helderheid" };
            panel.Children.Add(control);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(string.Empty, UIElementAutomationPeer.CreatePeerForElement(control.Slider).GetAutomationId());
            Assert.Null(control.Slider.ToolTip);
            Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(control.Slider)));
        });
    }

    // The value goes both ways, within the range and steps given.
    [Fact]
    public void Value_GoesToTheSliderAndBack()
    {
        AppTheme.Run(panel =>
        {
            var control = new ValueSlider { Minimum = 10, Maximum = 300, SmallChange = 5, LargeChange = 25, TickFrequency = 5, Value = 100 };
            panel.Children.Add(control);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(100, control.Slider.Value);
            Assert.Equal(10, control.Slider.Minimum);
            Assert.Equal(300, control.Slider.Maximum);
            Assert.True(control.Slider.IsSnapToTickEnabled);

            control.Slider.Value = 150;

            Assert.Equal(150, control.Value);
        });
    }

    // A fill colors the slider only, not its name; without one the slider takes that of its style again.
    [Fact]
    public void FillBrush_ColorsOnlyTheSliderAndGoesBackToTheStyle()
    {
        AppTheme.Run(panel =>
        {
            var control = new ValueSlider { Label = "Rood" };
            panel.Children.Add(control);
            OffscreenWindow.WaitUntilIdle();
            var styled = control.Slider.Foreground;
            var label = AppTheme.Descendants<TextBlock>(control).Last();
            var labelBrush = label.Foreground;

            control.FillBrush = Brushes.Red;

            Assert.Same(Brushes.Red, control.Slider.Foreground);
            Assert.Same(labelBrush, label.Foreground);

            control.FillBrush = null;

            Assert.Same(styled, control.Slider.Foreground);
        });
    }
}
