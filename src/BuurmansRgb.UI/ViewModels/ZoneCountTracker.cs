using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Which count of LEDs the engine runs now, as far as this settings app knows, and whether it is the
// count this app started: PreviewViewModel shows what this says and asks it before it stops a count
// by itself. The engine reports a count from more than one thread, and another settings app may
// count a zone at the same time, so the order of the states is subtle: see Take. No UI; used on the
// UI thread.
internal sealed class ZoneCountTracker
{
    // The sequence of the newest count the engine reported (ZoneCountInfo.Sequence).
    private long _countSequence;

    // The zone this app last started to count, until it asks to stop it, keeps its length ("Dit is
    // de laatste") or the engine ends it: only that count stops by itself. Another settings app may
    // count a zone at the same time, also the same zone after this count ended.
    private (string DeviceId, int ZoneIndex)? _ownCount;

    // The sequence of the first state in which the engine reported the count of this app; null
    // until then. Only a state after it that no longer counts that zone ends the count of this
    // app: a None from before the start can still be on its way.
    private long? _ownCountSequence;

    // The sequence of the newest state when this app started its count: a state up to it is from
    // before the start, so it does not confirm the count, such as a step of the same zone that
    // another settings app counted.
    private long _ownCountStartedAfter;

    // The zone whose count was stopped here, until the engine reports a state that no longer
    // counts it: a step the engine took just before the stop reached it is ignored meanwhile.
    private (string DeviceId, int ZoneIndex)? _stoppedCount;

    // The count as the engine reported it last, also while its zone is not in view; None after a
    // stop here.
    public ZoneCountInfo Count { get; private set; } = ZoneCountInfo.None;

    // Forgets the count of a connection that went, with its sequence: a new engine starts again.
    public void Forget()
    {
        _countSequence = 0;
        _stoppedCount = null;
        ForgetOwnCount();
        Count = ZoneCountInfo.None;
    }

    // The engine refused a start of this app, such as for a zone the list here still shows: the
    // count is not this app's, unless the engine confirmed it meanwhile (a start after it).
    public void ForgetStart(string deviceId, int zoneIndex)
    {
        if (_ownCount == (deviceId, zoneIndex) && _ownCountSequence is null)
        {
            ForgetOwnCount();
        }
    }

    // The count of LEDs as the engine reports it; false when it is dropped, so Count stays. A state
    // older than the last one is dropped: the engine reports a count from more than one thread, so
    // a step can arrive after the pause that followed it. After a stop here, states of the stopped
    // zone wait for the engine to end its count; their sequence is not taken, so its None still
    // comes in.
    public bool Take(ZoneCountInfo count)
    {
        if (count.Sequence < _countSequence)
        {
            return false;
        }

        // States can arrive out of order here too: a step of a new count of the stopped zone (such
        // as one another settings app starts) that overtakes the None is ignored, and then the None
        // hides the count. That misses one tick; the next step shows the count again.
        if (_stoppedCount is { } stopped)
        {
            if (count.Counts(stopped.DeviceId, stopped.ZoneIndex))
            {
                return false;
            }

            _stoppedCount = null;
        }

        _countSequence = count.Sequence;
        FollowOwnCount(count);
        Count = count;
        return true;
    }

    // Whether the count of this zone is the one this app started.
    public bool IsOwn(string deviceId, int zoneIndex) => _ownCount == (deviceId, zoneIndex);

    // This app asks the engine to count the zone.
    public void Start(string deviceId, int zoneIndex)
    {
        _ownCount = (deviceId, zoneIndex);
        _ownCountSequence = null;
        _ownCountStartedAfter = _countSequence;
    }

    // A new length of the counted zone, such as from "Dit is de laatste", ends its count in the
    // engine.
    public void Resize(string deviceId, int zoneIndex)
    {
        if (IsOwn(deviceId, zoneIndex))
        {
            ForgetOwnCount();
        }
    }

    // This app asks the engine to stop counting the zone. The count ends here at once when the
    // engine counts that zone, with true; the engine confirms it with ZoneCountInfo.None, and its
    // states of the zone are ignored until then (Take).
    public bool Stop(string deviceId, int zoneIndex)
    {
        if (IsOwn(deviceId, zoneIndex))
        {
            ForgetOwnCount();
        }

        if (!Count.Counts(deviceId, zoneIndex))
        {
            return false;
        }

        _stoppedCount = (deviceId, zoneIndex);
        Count = ZoneCountInfo.None;
        return true;
    }

    // The same, but only for a count of this zone that this app started and the engine counts, such
    // as when its zone goes from view; false leaves everything as it was.
    public bool StopOwn(string deviceId, int zoneIndex) =>
        Count.Counts(deviceId, zoneIndex) && IsOwn(deviceId, zoneIndex) && Stop(deviceId, zoneIndex);

    // The count of this app is no longer its own, such as at a pause of the effects: that ends any
    // count in the engine, and refuses a start that reaches it after the pause, so a start the
    // engine refused would otherwise never be confirmed nor ended.
    public void ForgetOwnCount()
    {
        _ownCount = null;
        _ownCountSequence = null;
    }

    // The engine confirms the count of this app, or ends it by itself, such as at a pause, at
    // Verversen or with a new list of devices.
    private void FollowOwnCount(ZoneCountInfo count)
    {
        if (_ownCount is not { } own || count.Sequence <= _ownCountStartedAfter)
        {
            return;
        }

        if (count.Counts(own.DeviceId, own.ZoneIndex))
        {
            _ownCountSequence ??= count.Sequence;
        }
        else if (count.Sequence > _ownCountSequence)
        {
            ForgetOwnCount();
        }
    }
}
