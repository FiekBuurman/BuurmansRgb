using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Programs;

// Whether other RGB software runs that may overwrite the colors (OtherRgbSoftwareList), so the app
// can say so. It looks only when asked: at the start of the engine, after waking up (resume, once
// the lights are back) and at "Verversen" in the app (EngineApi.RescanDevicesAsync), each time with
// one snapshot of the process list; no timer, no polling. Software that starts in between shows at
// the next of those moments. The user can dismiss a program: it is not shown again, also not when
// it stops and comes back later, until its id leaves Dismissed (settings.json, by hand). Thread-safe.
internal sealed class OtherRgbSoftwareCheck : IDisposable
{
    private readonly IProcessSnapshot _processes;
    private readonly IResumeEvents? _resume;
    private readonly Lock _gate = new();

    // One check at a time, so an older list never replaces a newer one.
    private readonly Lock _checkGate = new();
    private IReadOnlyList<OtherRgbSoftware> _found = [];
    private HashSet<string> _dismissed = new(StringComparer.Ordinal);

    // resume is null in tests that do not wake up.
    public OtherRgbSoftwareCheck(IProcessSnapshot processes, IResumeEvents? resume = null)
    {
        _processes = processes;
        _resume = resume;
        _resume?.Restored += OnRestored;
    }

    // Raised when Shown may have changed: other software found, or a program dismissed. On the
    // thread of the check or of the change.
    public event EventHandler? Changed;

    // Raised when Dismissed changed, through Dismiss or a set that differs, so the host keeps it
    // in the settings file, as it does for the other options.
    public event EventHandler? DismissedChanged;

    // What the last check found, in the order of OtherRgbSoftwareList.All.
    public IReadOnlyList<OtherRgbSoftware> Found
    {
        get
        {
            lock (_gate)
            {
                return _found;
            }
        }
    }

    // What the app shows: what was found, without what the user dismissed.
    public IReadOnlyList<OtherRgbSoftware> Shown
    {
        get
        {
            lock (_gate)
            {
                return [.. _found.Where(software => !_dismissed.Contains(software.Id))];
            }
        }
    }

    // The ids the user dismissed, in order; also ids this engine does not know, such as from a
    // newer version, so the file keeps them. Set from settings.json at the start and after a
    // change by hand; another set raises DismissedChanged and Changed.
    public IReadOnlyList<string> Dismissed
    {
        get
        {
            lock (_gate)
            {
                return [.. _dismissed.Order(StringComparer.Ordinal)];
            }
        }

        set
        {
            bool changed;
            lock (_gate)
            {
                var dismissed = new HashSet<string>(value.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
                changed = !dismissed.SetEquals(_dismissed);
                _dismissed = dismissed;
            }

            if (changed)
            {
                DismissedChanged?.Invoke(this, EventArgs.Empty);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // Not shown again from now on, also not after a restart (the host saves it). Throws for an id
    // that is not in OtherRgbSoftwareList, so a typo from the app does not land in the file.
    public void Dismiss(string id)
    {
        if (!OtherRgbSoftwareList.IsKnown(id))
        {
            throw new ArgumentException($"No RGB software is known by the id '{id}'.", nameof(id));
        }

        bool added;
        lock (_gate)
        {
            added = _dismissed.Add(id);
        }

        if (added)
        {
            Trace.TraceInformation($"Other RGB software dismissed: {id}.");
            DismissedChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // One look at the process list. Raises Changed when what was found changed. A list that does
    // not read keeps what was found before: no message is better than one that comes and goes.
    // The engine log tells what was found and how long it took, once per check.
    public void Check()
    {
        bool changed;
        lock (_checkGate)
        {
            var watch = Stopwatch.StartNew();
            IReadOnlyList<OtherRgbSoftware> found;
            try
            {
                found = OtherRgbSoftwareList.FindIn(_processes.Take());
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"The process list could not be read for other RGB software: {exception.Message}");
                return;
            }

            lock (_gate)
            {
                changed = !found.SequenceEqual(_found);
                _found = found;
            }

            var names = found.Count > 0 ? string.Join(", ", found.Select(software => software.Name)) : "none";
            Trace.TraceInformation($"Other RGB software checked in {watch.Elapsed.TotalMilliseconds:0.0} ms: {names}.");
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _resume?.Restored -= OnRestored;

    // On the thread of the timer of ResumeRestorer, so not on the tray thread.
    private void OnRestored(object? sender, DateTimeOffset? asleepSince) => Check();
}
