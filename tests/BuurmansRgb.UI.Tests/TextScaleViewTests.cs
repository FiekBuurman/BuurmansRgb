using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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

    // A board with an addressable header that can be counted, as on Kleur.
    private static readonly DeviceInfo Board = new("board", "ASUS ROG STRIX B550-F GAMING", DeviceType.Motherboard, 8,
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 6, 0, 60)]) { Source = DeviceSource.Hardware };

    // On each of the four pages, also with the panels that come and go (counting the LEDs of a zone,
    // the message under the hex code and the settings of a scene among them), no text is cut off or lies
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
            Check(AppPage.Color, "melding onder de hex-code", () =>
            {
                var box = PageViews.Named<TextBox>(window, Strings.HexLabel);
                var color = viewModel.ColorPicker.Color.ToHex();
                Assert.True(box.Focus());
                box.Text = "#12";
                Assert.True(PageViews.Named<Slider>(window, Strings.ChannelRed).Focus());
                OffscreenWindow.WaitUntilIdle();
                Assert.Contains(AppTheme.Descendants<TextBlock>(window), text => text.IsVisible && text.Text == UiText.Format(Strings.HexCodeInvalidKept, "#12", color));
            });
            Check(AppPage.Color, "bord, tellen bij een zone", () =>
            {
                viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "board");
                OffscreenWindow.WaitUntilIdle();
                PageViews.Click(PageViews.ButtonNamed(window, string.Format(Strings.CountZoneAccessibleName, Board.Name, "ARGB 1")));
                Assert.NotNull(StopCounting());
            });
            PageViews.Click(StopCounting());
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

            Button StopCounting() => AppTheme.Descendants<Button>(window).Single(button => button.IsVisible && Equals(button.Content, Strings.ZoneCountStop));
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
            using var sizes = TextSizes.Follow(window.Resources, textScale);
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

    // A name of a device too long for the page: the choice on Kleur ends it in "…" within the page,
    // with its switch beside it, at 100 % as at the largest text size.
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    public async Task LongDeviceName_EndsInAnEllipsisWithinThePage(double factor)
    {
        const string name = "Corsair iCUE LINK System Hub met de hele lange naam die iemand in OpenRGB gaf";
        using var viewModel = await PageViews.ConnectAsync(api =>
            api.Devices = [TestDevices.Create("hub", name, DeviceType.LedStrip, 30)]);

        Show(viewModel, factor, window =>
        {
            viewModel.Navigation.GoTo(AppPage.Color);
            viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "hub");
            OffscreenWindow.WaitUntilIdle();
            var page = (FrameworkElement)window.Content;
            var choice = PageViews.Named<ComboBox>(page, Strings.LampChoiceLabel);
            var presenter = Ancestor<ScrollContentPresenter>(choice);
            var selection = AppTheme.Descendants<TextBlock>(choice).First(text => text.Inlines.OfType<System.Windows.Documents.Run>().Any(run => run.Text == name));
            var toggle = AppTheme.Descendants<ToggleButton>(choice.Parent).Single(button => button.TemplatedParent is null && button.IsVisible);
            var problems = TextFit.Problems(page);

            Assert.True(Right(toggle, presenter) <= presenter.ActualWidth + 0.5, $"The switch ends at {Right(toggle, presenter):0.#}, past the {presenter.ActualWidth:0.#} of the page.");
            Assert.True(Right(choice, presenter) <= toggle.TranslatePoint(default, presenter).X, "The choice lies under its switch.");
            Assert.True(Right(selection, presenter) <= Right(choice, presenter), "The name runs past its choice.");
            Assert.Equal(TextTrimming.CharacterEllipsis, selection.TextTrimming);
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        });

        static double Right(FrameworkElement element, UIElement ancestor) => element.TranslatePoint(new Point(element.ActualWidth, 0), ancestor).X;

        static T Ancestor<T>(DependencyObject element)
            where T : DependencyObject
        {
            for (var parent = VisualTreeHelper.GetParent(element); ; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is T found)
                {
                    return found;
                }
            }
        }
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
        using var sizes = TextSizes.Follow(dialog.Resources, new FakeTextScale { Factor = factor });
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
            Board,
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
        using var sizes = TextSizes.Follow(window.Resources, new FakeTextScale { Factor = factor });
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
