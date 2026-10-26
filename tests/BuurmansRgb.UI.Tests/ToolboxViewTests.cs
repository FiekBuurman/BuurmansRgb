using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Uit de gereedschapskist" on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class ToolboxViewTests
{
    // Squares of 32 px instead of the 44 of the design, all nine in one row in a wide window; the
    // tiles of a list of colors keep their 44.
    [Fact]
    public async Task Presets_AreSmallSquaresInOneRow()
    {
        using var viewModel = await ConnectAsync();

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 1400;
            var page = Show(panel, viewModel);
            var tiles = PresetTiles(page);

            Assert.Equal(ColorPickerViewModel.Presets.Count, tiles.Count);
            Assert.All(tiles, tile => Assert.Equal((32.0, 32.0), (tile.ActualWidth, tile.ActualHeight)));
            Assert.Single(tiles.Select(tile => tile.TranslatePoint(default, page).Y).Distinct());
        });
    }

    // Each tile takes the focus and has its Dutch name for a screen reader; a click mixes its color.
    [Fact]
    public async Task Presets_TakeTheFocusHaveTheirNameAndMixTheirColor()
    {
        using var viewModel = await ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = Show(panel, viewModel);
            var tiles = PresetTiles(page);

            Assert.Equal(ColorPickerViewModel.Presets.Select(preset => preset.Name), tiles.Select(AutomationProperties.GetName));
            Assert.True(tiles[8].Focus());
            Assert.True(tiles[8].IsKeyboardFocused);

            Click(tiles[8]);

            Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
            Assert.Equal(true, tiles[8].Tag);
        });
    }

    internal static async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(new FakeEngineConnector(), api => arrange?.Invoke(api)));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }

    internal static ColorPage Show(StackPanel panel, MainViewModel viewModel)
    {
        var page = new ColorPage { DataContext = viewModel };
        panel.Children.Add(page);
        OffscreenWindow.WaitUntilIdle();
        return page;
    }

    internal static List<Button> PresetTiles(DependencyObject page) =>
        [.. AppTheme.Descendants<Button>(page).Where(button => button.DataContext is ColorPreset)];

    // As a screen reader or a click would: only an enabled button does something.
    internal static void Click(Button button)
    {
        var invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
        invoke.Invoke();
        OffscreenWindow.WaitUntilIdle();
    }
}
