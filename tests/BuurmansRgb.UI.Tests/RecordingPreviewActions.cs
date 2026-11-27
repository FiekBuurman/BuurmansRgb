using System.Collections;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// A flash is recorded as the id of its device, clearing the selection as "clear". Also the requests
// a PreviewViewModel is made with in a test.
internal sealed class RecordingPreviewActions : IPreviewActions
{
    public List<object> Requests { get; } = [];

    public void ClickLed(LedClick click) => Requests.Add(click);

    public void SelectLeds(LedSelection selection) => Requests.Add(selection);

    public void ClearLedSelection() => Requests.Add("clear");

    public void ResizeZone(ZoneResize resize) => Requests.Add(resize);

    public void FlashDevice(string deviceId) => Requests.Add(deviceId);

    public void FlashZone(ZoneFlash flash) => Requests.Add(flash);

    public void SetZonesAlike(ZonesAlikeChoice choice) => Requests.Add(choice);

    public void StartZoneCount(ZoneCountStart start) => Requests.Add(start);

    public void ChangeZoneCount(ZoneCountChangeRequest request) => Requests.Add(request);

    public void StopZoneCount(ZoneCountStop stop) => Requests.Add(stop);

    public bool CanFlash { get; set; } = true;

    // The requests of one kind, also those that come after this call.
    public IReadOnlyList<T> Of<T>() => new RequestsOf<T>(Requests);

    private sealed class RequestsOf<T>(List<object> requests) : IReadOnlyList<T>
    {
        public int Count => requests.OfType<T>().Count();

        public T this[int index] => requests.OfType<T>().ElementAt(index);

        public IEnumerator<T> GetEnumerator() => requests.OfType<T>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
