using System.Windows.Automation;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The buttons to share on the real Scènes and Kleur pages, in a window off screen: each with a name
// a screen reader reads and the same plain words as on the button, and what a click does.
[Collection(AppThemeCollection.Name)]
public sealed class SharingViewTests
{
    // "Importeren…" and "Code plakken" below the cards; "Exporteren…" and "Code kopiëren" at the
    // chosen scene, for that scene.
    [Fact]
    public async Task ScenesPage_HasTheButtonsAndExportsTheChosenScene()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectedViewModel.CreateAsync(api =>
        {
            engine = api;
            api.Profiles.Add("Avond");
            api.ProfileLighting["Avond"] = new LightingInfo(Orange, 0.8, EffectIds.Static);
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            var import = PageViews.ButtonNamed(page, Strings.ShareImportAccessibleName);
            var paste = PageViews.ButtonNamed(page, Strings.SharePasteCodeAccessibleName);
            Assert.Equal((Strings.ShareImport, Strings.SharePasteCode), (import.Content, paste.Content));
            Assert.True(import.IsEnabled);

            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Avond")));
            var copy = PageViews.ButtonNamed(page, Strings.ShareCopySceneCodeAccessibleName);
            Assert.Equal(Strings.ShareExport, PageViews.ButtonNamed(page, Strings.ShareExportSceneAccessibleName).Content);

            PageViews.Click(copy);

            Assert.Contains("export scene Avond", engine!.Calls);
            Assert.Equal(UiText.Format(Strings.ShareCodeCopied, "Avond"), viewModel.StatusMessage);
        });
    }

    // At an own effect, for that effect; below the effect choice the import.
    [Fact]
    public async Task ColorPage_HasTheButtonsAndCopiesTheCodeOfTheEffect()
    {
        FakeEngineApi? engine = null;
        var disco = new CustomEffect("custom:disco", "Disco", [new CustomEffectStep(Red)]);
        using var viewModel = await ConnectedViewModel.CreateAsync(api =>
        {
            engine = api;
            api.Effects = TestEffects.StaticAndRainbow;
            api.CustomEffects = [disco];
            api.Lighting[""] = api.Lighting[""] with { EffectId = disco.Id };
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            Assert.True(PageViews.ButtonNamed(page, Strings.ShareImportAccessibleName).IsVisible);
            Assert.True(PageViews.ButtonNamed(page, Strings.SharePasteCodeAccessibleName).IsVisible);
            var export = PageViews.ButtonNamed(page, Strings.ShareExportEffectAccessibleName);
            Assert.Equal(Strings.ShareExportEffectToolTip, export.ToolTip);

            PageViews.Click(PageViews.ButtonNamed(page, Strings.ShareCopyEffectCodeAccessibleName));

            Assert.Contains("export effect custom:disco", engine!.Calls);
            Assert.Equal(UiText.Format(Strings.ShareCodeCopied, "Disco"), viewModel.StatusMessage);
            Assert.Equal(Strings.ShareCopyCode, PageViews.ButtonNamed(page, Strings.ShareCopyEffectCodeAccessibleName).Content);
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(export)));
        });
    }
}
