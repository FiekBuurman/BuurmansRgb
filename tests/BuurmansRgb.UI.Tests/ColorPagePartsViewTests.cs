using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The parts of Kleur in a control of their own, "Jouw effect" (CustomEffectEditor) and the mixing panel
// (ColorMixer), each on its own in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class ColorPagePartsViewTests
{
    // Hidden, as the page has them with another effect, their elements are there all the same, as they
    // were on the page; a UserControl would only build them once it shows. Neither adds an element of
    // its own for a screen reader.
    [Fact]
    public void Parts_WhileHidden_HaveTheirElementsAndNoAutomationElementOfTheirOwn()
    {
        AppTheme.Run(panel =>
        {
            var editor = AppTheme.Show(panel, new CustomEffectEditor { Visibility = Visibility.Collapsed });
            var mixer = AppTheme.Show(panel, new ColorMixer { Visibility = Visibility.Collapsed });

            Assert.NotNull(AppTheme.Part<ItemsControl>(editor, "StepTiles"));
            Assert.NotNull(AppTheme.Part<ComboBox>(editor, "CustomEffectChoice"));
            Assert.NotNull(AppTheme.Part<ItemsControl>(mixer, "CustomColorTiles"));
            Assert.NotNull(AppTheme.Part<Button>(mixer, "AddCustomColor"));
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(editor));
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(mixer));
        });
    }
}
