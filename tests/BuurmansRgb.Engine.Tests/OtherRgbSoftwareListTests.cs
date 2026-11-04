using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class OtherRgbSoftwareListTests
{
    private const int ServiceSession = RunningProcess.ServiceSessionId;
    private const int UserSession = FakeProcessSnapshot.UserSession;

    [Fact]
    public void FindIn_NothingOfThem_FindsNothing() =>
        Assert.Empty(OtherRgbSoftwareList.FindIn([new("explorer", UserSession), new("svchost", ServiceSession)]));

    // The name of the exe without ".exe", as Process.ProcessName gives it, in any case.
    [Theory]
    [InlineData("SignalRgbService", "signalrgb")]
    [InlineData("Corsair.Service", "icue")]
    [InlineData("iCUE", "icue")]
    [InlineData("Razer Synapse Service", "razer")]
    [InlineData("RzSDKService", "razer")]
    [InlineData("LightingService", "armourycrate")]
    [InlineData("lightingservice", "armourycrate")]
    [InlineData("LEDKeeper2", "mysticlight")]
    [InlineData("RGBFusion", "rgbfusion")]
    public void FindIn_AProcessOfTheList_FindsItsSoftware(string process, string id) =>
        Assert.Equal(id, Assert.Single(OtherRgbSoftwareList.FindIn([new(process, UserSession)])).Id);

    // A service is found as well as an app: most of them run as a service in session 0.
    [Fact]
    public void FindIn_AServiceInSessionZero_FindsIt() =>
        Assert.Equal("icue", Assert.Single(OtherRgbSoftwareList.FindIn([new("Corsair.Service", ServiceSession)])).Id);

    [Fact]
    public void FindIn_SeveralProcessesOfOneProgram_FindsItOnce() =>
        Assert.Single(OtherRgbSoftwareList.FindIn([new("Corsair.Service", ServiceSession), new("CueLLAccessService", ServiceSession), new("iCUE", UserSession)]));

    // In the order of the list, whatever the order of the processes.
    [Fact]
    public void FindIn_SeveralPrograms_FindsThemInTheOrderOfTheList()
    {
        var found = OtherRgbSoftwareList.FindIn([new("LEDKeeper2", UserSession), new("SignalRgb", UserSession), new("iCUE", UserSession)]);

        Assert.Equal(["signalrgb", "icue", "mysticlight"], found.Select(software => software.Id));
        Assert.Equal(["SignalRGB", "Corsair iCUE", "MSI Mystic Light"], found.Select(software => software.Name));
    }

    // The service OpenRGB alone is the one the engine uses.
    [Fact]
    public void FindIn_OnlyTheServiceOpenRgb_FindsNothing() =>
        Assert.Empty(OtherRgbSoftwareList.FindIn([new("OpenRGB", ServiceSession)]));

    // OpenRGB started by hand, without the service (as for developers), is the server itself.
    [Fact]
    public void FindIn_OnlyAnOpenRgbOfTheUser_FindsNothing() =>
        Assert.Empty(OtherRgbSoftwareList.FindIn([new("OpenRGB", UserSession)]));

    [Fact]
    public void FindIn_AnOpenRgbOfTheUserNextToTheService_FindsASecondOpenRgb() =>
        Assert.Equal(OtherRgbSoftware.OpenRgbId, Assert.Single(OtherRgbSoftwareList.FindIn([new("OpenRGB", ServiceSession), new("openrgb", 2)])).Id);

    // The ids are kept in settings.json, so they must not change, nor be the same twice; the
    // process names neither: one process belongs to one program.
    [Fact]
    public void All_HasTheIdsThatSettingsKeep()
    {
        Assert.Equal(["signalrgb", "icue", "razer", "armourycrate", "mysticlight", "rgbfusion", "openrgb"], OtherRgbSoftwareList.All.Select(entry => entry.Id));
        var processes = OtherRgbSoftwareList.All.SelectMany(entry => entry.Processes).ToList();
        Assert.Equal(processes.Count, processes.Distinct(ProgramNames.Comparer).Count());
        Assert.All(processes, process => Assert.EndsWith(".exe", process, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("signalrgb", true)]
    [InlineData("openrgb", true)]
    [InlineData("SignalRGB", false)]
    [InlineData("gimp", false)]
    public void IsKnown_OnlyForTheIdsOfTheList(string id, bool known) => Assert.Equal(known, OtherRgbSoftwareList.IsKnown(id));
}
