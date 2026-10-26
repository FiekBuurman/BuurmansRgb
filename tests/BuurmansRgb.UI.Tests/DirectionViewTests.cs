using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Richting" next to the speed on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class DirectionViewTests
{
    // Next to the speed, with its name and how the degrees go for a screen reader.
    [Fact]
    public async Task Slider_Rainbow_ShowsNextToTheSpeedWithItsNameAndHelp()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Rainbow);

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 1400;
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var direction = PageViews.Named<Slider>(page, Strings.DirectionHeader);
            var speed = PageViews.Named<Slider>(page, Strings.SpeedHeader);

            Assert.True(direction.IsVisible);
            Assert.Equal(Strings.DirectionHelp, AutomationProperties.GetHelpText(direction));
            Assert.Equal(speed.TranslatePoint(default, page).Y, direction.TranslatePoint(default, page).Y, 1);
            Assert.True(direction.TranslatePoint(default, page).X > speed.TranslatePoint(default, page).X);
            Assert.False(ClearButton(page).IsVisible);
        });
    }

    [Fact]
    public async Task Slider_StaticEffect_IsHidden()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            Assert.False(PageViews.Named<Slider>(page, Strings.DirectionHeader).IsVisible);
        });
    }

    // Moving the slider shows the way to go back to the order of the LEDs, which hides itself again.
    [Fact]
    public async Task ClearButton_AfterMovingTheSlider_ShowsAndTakesTheDirectionAway()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Wave);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            PageViews.Named<Slider>(page, Strings.DirectionHeader).Value = 45;
            OffscreenWindow.WaitUntilIdle();
            var clear = ClearButton(page);

            Assert.True(clear.IsVisible);
            Assert.Equal("45°", viewModel.DirectionText);

            PageViews.Click(clear);

            Assert.False(viewModel.HasDirection);
            Assert.False(clear.IsVisible);
        });
    }

    // The button hides itself with Enter; the focus goes to the slider instead of the top of the window.
    [Fact]
    public async Task ClearButton_Enter_GivesTheFocusToTheSlider()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Wave);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var slider = PageViews.Named<Slider>(page, Strings.DirectionHeader);
            slider.Value = 90;
            OffscreenWindow.WaitUntilIdle();
            var clear = ClearButton(page);
            Assert.True(clear.Focus());

            clear.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(clear), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
            OffscreenWindow.WaitUntilIdle();

            Assert.False(viewModel.HasDirection);
            Assert.False(clear.IsVisible);
            Assert.True(slider.IsKeyboardFocused);
        });
    }

    // A screen reader hears "Volgorde" rather than the 0 of the slider, and the degrees once there are.
    [Fact]
    public async Task Slider_ItemStatus_SaysTheOrderOrTheDegrees()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Wave);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var slider = PageViews.Named<Slider>(page, Strings.DirectionHeader);

            Assert.Equal(Strings.DirectionAlongLeds, AutomationProperties.GetItemStatus(slider));

            slider.Value = 270;
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("270°", AutomationProperties.GetItemStatus(slider));
        });
    }

    // At 0 Home changes no value; without a direction it chooses 0°.
    [Fact]
    public async Task Slider_HomeWithoutDirection_ChoosesZeroDegrees()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Wave);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var slider = PageViews.Named<Slider>(page, Strings.DirectionHeader);
            Assert.True(slider.Focus());

            slider.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(slider), 0, Key.Home) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            OffscreenWindow.WaitUntilIdle();

            Assert.True(viewModel.HasDirection);
            Assert.Equal("0°", AutomationProperties.GetItemStatus(slider));
            Assert.True(ClearButton(page).IsVisible);
        });
    }

    // So does a click on the knob, which moves it nowhere.
    [Fact]
    public async Task Slider_ClickOnTheKnobWithoutDirection_ChoosesZeroDegrees()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Wave);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var thumb = AppTheme.Descendants<Thumb>(PageViews.Named<Slider>(page, Strings.DirectionHeader)).Single();

            thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: false));
            OffscreenWindow.WaitUntilIdle();

            Assert.True(viewModel.HasDirection);
            Assert.Equal(0.0, viewModel.Direction);
        });
    }

    // A drag that moved the knob already sent its degrees; letting go sends nothing more.
    [Fact]
    public async Task Slider_DragCompletedAfterMoving_SendsOnlyTheDegrees()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectWithAsync(EffectIds.Wave, api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var slider = PageViews.Named<Slider>(page, Strings.DirectionHeader);
            slider.Value = 30;
            OffscreenWindow.WaitUntilIdle();

            AppTheme.Descendants<Thumb>(slider).Single().RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: false));
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(["direction all 30"], engine!.Calls);
        });
    }

    // A drag that was canceled chooses nothing.
    [Fact]
    public async Task Slider_CanceledDragWithoutDirection_ChoosesNothing()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectWithAsync(EffectIds.Wave, api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var thumb = AppTheme.Descendants<Thumb>(PageViews.Named<Slider>(page, Strings.DirectionHeader)).Single();

            thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: true));
            OffscreenWindow.WaitUntilIdle();

            Assert.False(viewModel.HasDirection);
            Assert.Empty(engine!.Calls);
        });
    }

    // With a direction, Home is left to the slider and sends no 0° of its own.
    [Fact]
    public async Task Slider_HomeWithDirection_SendsNothingOfItsOwn()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectWithAsync(EffectIds.Wave, api =>
        {
            engine = api;
            api.Lighting[""] = api.Lighting[""] with { Direction = 200 };
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var slider = PageViews.Named<Slider>(page, Strings.DirectionHeader);
            Assert.True(slider.Focus());

            slider.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(slider), 0, Key.Home) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(200.0, viewModel.Direction);
            Assert.Empty(engine!.Calls);
        });
    }

    private static Task<MainViewModel> ConnectWithAsync(string effectId, Action<FakeEngineApi>? arrange = null) => PageViews.ConnectAsync(api =>
    {
        api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.Wave];
        api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
        arrange?.Invoke(api);
    });

    private static Button ClearButton(DependencyObject page) =>
        PageViews.ButtonNamed(page, Strings.DirectionClearAccessibleName);
}
