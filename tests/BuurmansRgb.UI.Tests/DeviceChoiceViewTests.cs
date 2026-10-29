using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The choice of a device on Kleur, in a window off screen: the location label is muted, on the
// selected item of the open list in the muted color on the highlight, and the selection box above
// the list, which has no ComboBoxItem around it, does so without a binding error.
[Collection(AppThemeCollection.Name)]
public sealed class DeviceChoiceViewTests
{
    [Fact]
    public async Task DeviceChoice_MutesTheLocationWithoutABindingError()
    {
        using var viewModel = await PageViews.ConnectAsync();
        using var errors = BindingErrors.Listen();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var choice = AppTheme.Descendants<ComboBox>(page).First();
            viewModel.SelectedDevice = viewModel.Devices.First(device => device.DeviceId is not null);
            choice.IsDropDownOpen = true;
            OffscreenWindow.WaitUntilIdle();

            var items = viewModel.Devices.Select(device => Assert.IsType<ComboBoxItem>(choice.ItemContainerGenerator.ContainerFromItem(device))).ToList();
            Assert.NotEmpty(items);
            foreach (var item in items)
            {
                var expected = item.IsSelected ? "OnHighlightMutedTextBrush" : "TextMutedBrush";
                Assert.Same(page.FindResource(expected), Location(item).Foreground);
            }

            Assert.Contains(items, item => item.IsSelected);
            choice.IsDropDownOpen = false;
            OffscreenWindow.WaitUntilIdle();
            var selectionBox = AppTheme.Descendants<ContentPresenter>(choice).First(presenter => ReferenceEquals(presenter.Content, viewModel.SelectedDevice));
            Assert.Same(page.FindResource("TextMutedBrush"), Location(selectionBox).Foreground);
        });

        Assert.DoesNotContain(errors.Messages, message => message.Contains(nameof(ComboBoxItem), StringComparison.Ordinal));
    }

    // The second run of the text of a device: its location label.
    private static Run Location(System.Windows.DependencyObject parent) =>
        Assert.IsType<Run>(AppTheme.Descendants<TextBlock>(parent).First().Inlines.ElementAt(1));

    // What WPF writes about bindings while it listens, also when no debugger is attached.
    private sealed class BindingErrors : TraceListener
    {
        private readonly SourceLevels _level;

        private BindingErrors()
        {
            _level = PresentationTraceSources.DataBindingSource.Switch.Level;
        }

        public List<string> Messages { get; } = [];

        public static BindingErrors Listen()
        {
            PresentationTraceSources.Refresh();
            var errors = new BindingErrors();
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
            return errors;
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            lock (Messages)
            {
                Messages.Add(message ?? string.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
            PresentationTraceSources.DataBindingSource.Switch.Level = _level;
            base.Dispose(disposing);
        }
    }
}
