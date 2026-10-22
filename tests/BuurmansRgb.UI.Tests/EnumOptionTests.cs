using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class EnumOptionTests
{
    [Fact]
    public void Create_NamesTheModeFromTheResourcesOfItsType()
    {
        Assert.Equal(Strings.PausedLightsMode_Dim, EnumOption<PausedLightsMode>.Create(PausedLightsMode.Dim).DisplayName);
        Assert.Equal(Strings.SimulatedDevicesMode_Always, EnumOption<SimulatedDevicesMode>.Create(SimulatedDevicesMode.Always).ToString());
    }

    [Fact]
    public void Create_ModeWithoutAName_ShowsItsEnglishName()
    {
        var option = EnumOption<PausedLightsMode>.Create((PausedLightsMode)99);

        Assert.Equal("99", option.DisplayName);
        Assert.Equal("99", option.ToString());
    }
}
