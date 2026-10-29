using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The real MainWindow in its smallest size (960 × 640), off screen, with a fake text size of Windows
// on its resources (ContrastThemeViewTests says why there): the setting of Windows is never read or
// changed.
[Collection(AppThemeCollection.Name)]
public sealed class TextScaleViewTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(new RgbColor(255, 0, 0), 1, 0), new CustomEffectStep(new RgbColor(0, 0, 255), 1, 0)]);

    // On each of the four pages, also with the panels that come and go, no text is cut off or lies
    // over another text or a control, in the rail, the page and the status bar; a name in a card
    // may end in "…", as at 100 %. From the 100 % of Windows up to its largest, 225 %.
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    public async Task EveryPage_AtTheTextSize_CutsOffAndOverlapsNothing(double factor)
    {
        using var viewModel = await ConnectAsync();
        var problems = new List<string>();

        Show(viewModel, factor, window =>
        {
            void Check(AppPage page, string state, Action? arrange = null)
            {
                viewModel.Navigation.GoTo(page);
                arrange?.Invoke();
                OffscreenWindow.WaitUntilIdle();
                problems.AddRange(TextFit.Problems((FrameworkElement)window.Content).Select(problem => $"{page} {state}: {problem}"));
            }

            Check(AppPage.Lamps, "");
            Check(AppPage.Color, "alle apparaten");
            Check(AppPage.Color, "strip, kleurenloop", () =>
            {
                viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");
                viewModel.SelectedEffect = Effect(viewModel, EffectIds.ColorLoop);
            });
            Check(AppPage.Color, "strip, komeet", () => viewModel.SelectedEffect = Effect(viewModel, EffectIds.Comet));
            Check(AppPage.Color, "strip, per LED", () => viewModel.SelectedEffect = Effect(viewModel, EffectIds.PerLed));
            Check(AppPage.Color, "strip, eigen effect", () => viewModel.SelectedEffect = Effect(viewModel, Disco.Id));
            Check(AppPage.Scenes, "");
            Check(AppPage.Scenes, "Klusmodus aanpassen", () => viewModel.Profiles.EditCommand.Execute(viewModel.Profiles.Cards[0]));
            Check(AppPage.Settings, "");
        });

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // A page that shows takes another text size of Windows at once, and goes back: the text, the rail
    // and the hex code, whose field stays wide enough for a whole code and keeps its width while you
    // type one.
    [Fact]
    public async Task ShowingPage_FollowsAnotherTextSize()
    {
        using var viewModel = await ConnectAsync();

        Show(viewModel, 1.0, window =>
        {
            var textScale = new FakeTextScale();
            using var scale = TextScale.Follow(window.Resources, textScale);
            viewModel.Navigation.GoTo(AppPage.Color);
            OffscreenWindow.WaitUntilIdle();
            var page = (FrameworkElement)window.Content;
            var hint = AppTheme.Descendants<TextBlock>(page).First(text => text.Text == Strings.CurrentColorLabel);
            var box = PageViews.Named<TextBox>(page, Strings.HexLabel);
            var rail = AppTheme.Descendants<ListBox>(page).Single(list => System.Windows.Automation.AutomationProperties.GetName(list) == Strings.NavAccessibleName);
            Assert.Equal(13, hint.FontSize);
            Assert.Equal(180, box.ActualWidth);
            Assert.Equal(88, ((FrameworkElement)rail.Parent).ActualWidth);

            textScale.Set(2.0);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(26, hint.FontSize);
            Assert.Equal(60, box.FontSize);
            Assert.True(((FrameworkElement)rail.Parent).ActualWidth > 88);
            Assert.True(box.Focus());
            box.Text = "#";
            OffscreenWindow.WaitUntilIdle();
            var width = box.ActualWidth;
            box.Text = "#FFFFFF";
            box.CaretIndex = box.Text.Length;
            OffscreenWindow.WaitUntilIdle();
            Assert.Equal(width, box.ActualWidth);
            Assert.True(box.ExtentWidth <= box.ViewportWidth, $"The code is {box.ExtentWidth:0.0} px wide, the field has {box.ViewportWidth:0.0} px of {box.ActualWidth:0.0}.");
            Assert.Equal(0, box.HorizontalOffset);
            Assert.Null(LayoutInformation.GetLayoutClip(box));

            textScale.Set(1.0);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(13, hint.FontSize);
            Assert.Equal(180, box.ActualWidth);
            Assert.Equal(88, ((FrameworkElement)rail.Parent).ActualWidth);
        });
    }

    // The question before an own effect is deleted, the longest of the two, with its buttons beside
    // each other: nothing cut off or over another.
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    public void ConfirmDialog_AtTheTextSize_CutsOffAndOverlapsNothing(double factor) => AppTheme.Run(panel =>
    {
        var dialog = ConfirmDialog.ForCustomEffect(Window.GetWindow(panel)!, "Disco met een hele lange naam");
        dialog.ShowActivated = false;
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = OffscreenWindow.Offset;
        dialog.Top = OffscreenWindow.Offset;
        using var scale = TextScale.Follow(dialog.Resources, new FakeTextScale { Factor = factor });
        dialog.Show();
        OffscreenWindow.WaitUntilIdle();
        try
        {
            var problems = TextFit.Problems((FrameworkElement)dialog.Content);
            var buttons = AppTheme.Descendants<Button>(dialog).Where(button => button.Focusable).ToList();

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
            Assert.Equal(2, buttons.Count);
            Assert.Equal(buttons[0].TranslatePoint(default, dialog).Y, buttons[1].TranslatePoint(default, dialog).Y);
        }
        finally
        {
            dialog.Close();
        }
    });

    private static Task<MainViewModel> ConnectAsync() => PageViews.ConnectAsync(api =>
    {
        api.Devices =
        [
            TestDevices.Create("strip", "Corsair LS100 Smart Lighting Strip", DeviceType.LedStrip, 30),
            TestDevices.Create("ram", "Corsair Vengeance RGB PRO", DeviceType.Ram, 10),
            TestDevices.Create("board", "ASUS ROG STRIX B550-F GAMING", DeviceType.Motherboard, 8),
            TestDevices.Create("fan", "Fan", DeviceType.Fan, 12),
        ];
        api.Effects = [EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.ColorLoop, EffectIds.Comet, EffectIds.PerLed];
        api.CustomEffects = [Disco];
        api.Profiles.AddRange(["Klusmodus", "Filmavond"]);
        api.ProfileAutomations["Klusmodus"] = new ProfileAutomation(new TimeOnly(7, 30));
    });

    // MainWindow in its smallest size, off screen and not activated, at factor.
    private static void Show(MainViewModel viewModel, double factor, Action<Window> test) => AppTheme.Run(_ =>
    {
        var window = new MainWindow
        {
            DataContext = viewModel,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = OffscreenWindow.Offset,
            Top = OffscreenWindow.Offset,
            Width = 960,
            Height = 640,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        using var scale = TextScale.Follow(window.Resources, new FakeTextScale { Factor = factor });
        window.Show();
        try
        {
            test(window);
        }
        finally
        {
            window.Close();
        }
    });

    private static EffectOption Effect(MainViewModel viewModel, string id) => viewModel.Effects.Single(effect => effect.Id == id);
}
