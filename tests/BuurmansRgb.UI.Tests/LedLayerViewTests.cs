using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Per LED" and "Als laag boven het effect" under the chosen LEDs on Kleur, with "Laag weghalen",
// in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class LedLayerViewTests
{
    // Two radio buttons of one choice, named for a screen reader with what they do as help; Laag
    // weghalen is always there, grey with its tooltip while no chosen LED has a layer.
    [Fact]
    public async Task ChosenLeds_ShowWhereTheColorGoes()
    {
        using var viewModel = await ConnectedViewModel.CreateAsync(api => api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var perLed = (RadioButton)page.FindName("PaintPerLed");
            var layer = (RadioButton)page.FindName("PaintLayer");
            var remove = (Button)page.FindName("RemoveLayerButton");
            Assert.False(perLed.IsVisible);

            viewModel.Preview.Devices.Single(device => device.DeviceId == "strip").ClickLed(0, extend: false);
            OffscreenWindow.WaitUntilIdle();

            Assert.True(perLed.IsVisible);
            Assert.True(layer.IsVisible);
            Assert.True(perLed.IsChecked);
            Assert.False(layer.IsChecked);
            var peer = UIElementAutomationPeer.CreatePeerForElement(layer);
            Assert.Equal(AutomationControlType.RadioButton, peer.GetAutomationControlType());
            Assert.Equal(Strings.LedPaintLayerHelp, peer.GetHelpText());
            Assert.Equal(Strings.LedPaintPerLedHelp, AutomationProperties.GetHelpText(perLed));

            // In UI Automation the two are in the group "Waar komt de kleur?", which a screen reader names.
            var group = UIElementAutomationPeer.CreatePeerForElement((UIElement)page.FindName("LedPaintMode"));
            Assert.Equal(AutomationControlType.Group, group.GetAutomationControlType());
            Assert.Equal(Strings.LedPaintModeLabel, group.GetName());
            Assert.Equal([perLed, layer], group.GetChildren().OfType<UIElementAutomationPeer>().Select(child => child.Owner));
            Assert.Same(group, peer.GetParent());
            Assert.True(remove.IsVisible);
            Assert.False(remove.IsEnabled);
            Assert.True(ToolTipService.GetShowOnDisabled(remove));
            Assert.Equal(Strings.RemoveLayerToolTip, remove.ToolTip);
        });
    }

    // Choosing the layer for a screen reader, or with Space, is the same choice in the app; the hint
    // above says the color stays over every effect.
    [Fact]
    public async Task ChooseTheLayer_ChangesTheChoiceAndTheHint()
    {
        using var viewModel = await ConnectedViewModel.CreateAsync(api => api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            viewModel.Preview.Devices.Single(device => device.DeviceId == "strip").ClickLed(0, extend: false);
            OffscreenWindow.WaitUntilIdle();
            var layer = (RadioButton)page.FindName("PaintLayer");

            ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(layer).GetPattern(PatternInterface.SelectionItem)).Select();
            OffscreenWindow.WaitUntilIdle();

            Assert.True(viewModel.LedEditor.PaintsLayer);
            Assert.False(((RadioButton)page.FindName("PaintPerLed")).IsChecked);
            Assert.Contains(AppTheme.Descendants<TextBlock>(page), text => text.IsVisible && text.Text == Strings.LedLayerHint);
        });
    }

    // With the keyboard: Tab goes from Selectie opheffen to Per LED, Als laag boven het effect (each a
    // Tab stop, as the pace of Tellen) and then Laag weghalen once a chosen LED has a layer.
    [Fact]
    public async Task TheChoiceAndLaagWeghalen_AreReachedWithTab()
    {
        using var viewModel = await ConnectedViewModel.CreateAsync(api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed];
            api.Overlays["strip"] = [new RgbColor(255, 0, 0), null, null];
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            viewModel.Preview.Devices.Single(device => device.DeviceId == "strip").ClickLed(0, extend: false);
            OffscreenWindow.WaitUntilIdle();
            var clear = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.ClearLedSelection));
            var layer = (RadioButton)page.FindName("PaintLayer");
            var remove = (Button)page.FindName("RemoveLayerButton");
            Assert.True(remove.IsEnabled);
            Assert.True(layer.IsChecked);

            Assert.True(clear.Focus());
            clear.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            var first = Keyboard.FocusedElement;
            ((UIElement)first).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            var second = Keyboard.FocusedElement;
            ((UIElement)second).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            var third = Keyboard.FocusedElement;

            Assert.Same(page.FindName("PaintPerLed"), first);
            Assert.Same(layer, second);
            Assert.Same(remove, third);
        });
    }
}