using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// The texts are spelled out, so a change to them is a choice.
public sealed class DeviceRescanViewModelTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly DeviceRescanViewModel _rescan;
    private readonly List<int> _requests = [];

    public DeviceRescanViewModelTests()
    {
        _rescan = new DeviceRescanViewModel(_time);
        _rescan.Requested += (_, search) => _requests.Add(search);
    }

    public void Dispose() => _rescan.Dispose();

    [Fact]
    public void New_SaysVerversenAndHasNoMessage()
    {
        Assert.False(_rescan.IsSearching);
        Assert.Equal("Verversen", _rescan.ButtonText);
        Assert.Equal("Opnieuw verbinden", _rescan.ReconnectButtonText);
        Assert.False(_rescan.HasMessage);
        Assert.True(_rescan.RescanCommand.CanExecute(null));
    }

    // Both buttons run the one search, so both say so.
    [Fact]
    public void Rescan_SaysZoekenAndAsksOnce()
    {
        List<string?> changes = [];
        _rescan.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        _rescan.RescanCommand.Execute(null);

        Assert.True(_rescan.IsSearching);
        Assert.Equal("Zoeken…", _rescan.ButtonText);
        Assert.Equal("Zoeken…", _rescan.ReconnectButtonText);
        Assert.Contains(nameof(DeviceRescanViewModel.ReconnectButtonText), changes);
        Assert.Single(_requests);
    }

    // The button stays enabled, so the focus stays on it; a click while it searches does nothing.
    [Fact]
    public void Rescan_WhileSearching_DoesNotAskAgain()
    {
        _rescan.RescanCommand.Execute(null);

        Assert.True(_rescan.RescanCommand.CanExecute(null));
        _rescan.RescanCommand.Execute(null);

        Assert.Single(_requests);
    }

    [Fact]
    public void Finish_Reached_EndsWithoutMessage()
    {
        _rescan.RescanCommand.Execute(null);

        _rescan.Finish(_requests[0], RescanOutcome.Rescanned);

        Assert.False(_rescan.IsSearching);
        Assert.Equal("Verversen", _rescan.ButtonText);
        Assert.False(_rescan.HasMessage);
    }

    [Fact]
    public void Finish_NotReached_SaysOpenRgbCannotBeReached()
    {
        _rescan.RescanCommand.Execute(null);

        _rescan.Finish(_requests[0], RescanOutcome.Unreachable);

        Assert.False(_rescan.IsSearching);
        Assert.Equal("OpenRGB is niet bereikbaar, dus er is niet opnieuw naar apparaten gezocht. Kijk of OpenRGB draait met de SDK-server aan.", _rescan.Message);
    }

    [Fact]
    public void Finish_ListNotLoaded_SaysOpenRgbGaveNoDevices()
    {
        _rescan.RescanCommand.Execute(null);

        _rescan.Finish(_requests[0], RescanOutcome.ListNotLoaded, []);

        Assert.False(_rescan.IsSearching);
        Assert.Equal(
            "OpenRGB heeft gezocht, maar gaf zijn apparaten niet op tijd door. De verbinding wordt vanzelf opnieuw gemaakt. Draait er andere RGB-software, sluit die dan af. Komen je apparaten niet terug, kies dan later nog eens Verversen.",
            _rescan.Message);
    }

    // At home G HUB and Gigabyte Control Center held the devices while OpenRGB searched.
    [Fact]
    public void Finish_ListNotLoadedWithOtherRgbSoftware_NamesIt()
    {
        _rescan.RescanCommand.Execute(null);

        _rescan.Finish(_requests[0], RescanOutcome.ListNotLoaded, [new("ghub", "Logitech G HUB"), new("gcc", "Gigabyte Control Center")]);

        Assert.Equal(
            "OpenRGB heeft gezocht, maar gaf zijn apparaten niet op tijd door. Dat komt vaak door andere RGB-software die tegelijk draait: Logitech G HUB en Gigabyte Control Center. Sluit die af en kies weer Verversen.",
            _rescan.Message);
    }

    [Fact]
    public void Finish_ListNotLoadedWithASecondOpenRgb_NamesIt()
    {
        _rescan.RescanCommand.Execute(null);

        _rescan.Finish(_requests[0], RescanOutcome.ListNotLoaded, [new(OtherRgbSoftware.OpenRgbId, "OpenRGB")]);

        Assert.EndsWith("die tegelijk draait: de tweede OpenRGB. Sluit die af en kies weer Verversen.", _rescan.Message);
    }

    [Fact]
    public void OpenRgbConnected_ClearsTheListNotLoadedMessage()
    {
        _rescan.RescanCommand.Execute(null);
        _rescan.Finish(_requests[0], RescanOutcome.ListNotLoaded, [new("ghub", "Logitech G HUB")]);

        _rescan.OnOpenRgbConnected();

        Assert.False(_rescan.HasMessage);
    }

    [Fact]
    public void NoAnswerInTime_EndsTheSearchAndSaysItTakesLong()
    {
        _rescan.RescanCommand.Execute(null);

        _time.Advance(DeviceRescanViewModel.SearchTimeout - TimeSpan.FromMilliseconds(1));
        Assert.True(_rescan.IsSearching);
        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(_rescan.IsSearching);
        Assert.Equal("Het zoeken duurt lang. Apparaten die OpenRGB nog vindt, verschijnen vanzelf in de lijst.", _rescan.Message);
    }

    // The answer that still comes after the timeout tells how it went.
    [Fact]
    public void Finish_AfterTheTimeout_StillTellsHowItWent()
    {
        _rescan.RescanCommand.Execute(null);
        _time.Advance(DeviceRescanViewModel.SearchTimeout);

        _rescan.Finish(_requests[0], RescanOutcome.Rescanned);

        Assert.False(_rescan.HasMessage);
    }

    [Fact]
    public void Finish_OfAnOlderSearch_ChangesNothing()
    {
        _rescan.RescanCommand.Execute(null);
        _time.Advance(DeviceRescanViewModel.SearchTimeout);
        _rescan.RescanCommand.Execute(null);

        _rescan.Finish(_requests[0], RescanOutcome.Unreachable);

        Assert.True(_rescan.IsSearching);
        Assert.False(_rescan.HasMessage);
        Assert.Equal(2, _requests.Count);
    }

    [Fact]
    public void Rescan_AgainAfterAMessage_ClearsIt()
    {
        _rescan.RescanCommand.Execute(null);
        _rescan.Finish(_requests[0], RescanOutcome.Unreachable);

        _rescan.RescanCommand.Execute(null);

        Assert.False(_rescan.HasMessage);
    }

    [Fact]
    public void OpenRgbConnected_ClearsTheUnreachableMessage()
    {
        _rescan.RescanCommand.Execute(null);
        _rescan.Finish(_requests[0], RescanOutcome.Unreachable);

        _rescan.OnOpenRgbConnected();

        Assert.False(_rescan.HasMessage);
    }

    // That OpenRGB is connected says nothing about a search that took long.
    [Fact]
    public void OpenRgbConnected_KeepsTheSlowMessage()
    {
        _rescan.RescanCommand.Execute(null);
        _time.Advance(DeviceRescanViewModel.SearchTimeout);

        _rescan.OnOpenRgbConnected();

        Assert.True(_rescan.HasMessage);
    }

    [Fact]
    public void Clear_EndsTheSearchAndForgetsItsAnswer()
    {
        _rescan.RescanCommand.Execute(null);

        _rescan.Clear();
        _rescan.Finish(_requests[0], RescanOutcome.Unreachable);

        Assert.False(_rescan.IsSearching);
        Assert.False(_rescan.HasMessage);
        _time.Advance(DeviceRescanViewModel.SearchTimeout);
        Assert.False(_rescan.HasMessage);
    }
}
