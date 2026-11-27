using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// The scenes on Scènes: saving, loading, updating and deleting one, its devices and automation, the
// cards that show what each scene looks like and which one is on, and the times of "Alles uit" on
// Hele huis. A coordinator: it calls the engine of the session itself, an answer only shows while
// its connection is still the current one, and it tells MainViewModel what is for the status line
// (Message) and when a scene was loaded (Loaded). Used on the UI thread.
internal sealed class SceneCoordinator
{
    // The kind of call in the call queue that reads the scenes again; it changes nothing.
    internal const string ProfilesReloadCall = "profiles-reload";

    // The kind of call that saves the times of "Alles uit"; the newest times win.
    private const string WholeHouseTimesCall = "whole-house-times";

    private readonly EngineSession _session;
    private readonly ProfilesViewModel _profiles;
    private readonly WholeHouseTimesViewModel _wholeHouseTimes;

    // Counts the questions for the scene that is on, so an answer that a newer question overtook
    // does not bring back an older scene.
    private int _activeQuestions;

    public SceneCoordinator(EngineSession session, ProfilesViewModel profiles, WholeHouseTimesViewModel wholeHouseTimes)
    {
        _session = session;
        _profiles = profiles;
        _wholeHouseTimes = wholeHouseTimes;
        profiles.SaveRequested += (_, name) => SendProfileCall(name, api => api.SaveProfileAsync(name), Strings.ProfileSaved, isNew: true);
        profiles.LoadRequested += (_, name) => SendProfileCall(name, api => api.LoadProfileAsync(name), Strings.ProfileLoaded, done: () =>
            Loaded?.Invoke(this, EventArgs.Empty));
        profiles.DeleteRequested += (_, name) => SendProfileCall(name, api => api.DeleteProfileAsync(name), Strings.ProfileDeleted);
        profiles.UpdateRequested += (_, name) => SendProfileCall(name, api => api.SaveProfileAsync(name), Strings.SceneUpdated);
        // The boxes show the change already (SceneDevicesViewModel takes it as saved); when the
        // engine refuses it, they show again what the engine has.
        profiles.SceneDevices.ApplyRequested += (_, change) =>
            SendProfileCall(change.Name, api => api.SetProfileDevicesAsync(change.Name, change.DeviceIds), Strings.SceneDevicesSaved,
                failed: () => _ = ShowSceneDevicesAsync(change.Name));
        // The engine saves the automation as Normalize gives it, which the change already is.
        profiles.Automation.ApplyRequested += (_, change) =>
            SendProfileCall(change.Name, api => api.SetProfileAutomationAsync(change.Name, change.Automation), Strings.ProfileAutomationSaved, done: () =>
                profiles.Automation.Show(change.Name, change.Automation));
        profiles.AutomationNeeded += (_, name) => _ = ShowProfileAutomationAsync(name);
        wholeHouseTimes.ApplyRequested += (_, times) => SendWholeHouseTimes(times);
    }

    // For the status line, such as "Scène … geladen".
    public event EventHandler<string>? Message;

    // A scene was loaded, after the list and the message show it: MainViewModel shows its lighting.
    public event EventHandler? Loaded;

    // When connecting. The automation is fetched again from this engine, also for the scene that
    // stays selected after reconnecting. The names first, so they are there when reading the times
    // fails; the cards once both are read.
    public async Task LoadAsync(IEngineApi api)
    {
        _profiles.Automation.Clear();
        _profiles.Show(await api.GetProfilesAsync());
        ShowWholeHouseTimes(await api.GetWholeHouseTimesAsync());
        ShowCards();
    }

    // An imported scene is chosen on Scènes, so its block "… aanpassen" shows the devices it got. In
    // the queue with a key of its own, so the reload of ProfilesChanged, which keeps the scene that
    // is chosen, does not take its place. An own effect needs nothing: CustomEffectsChanged puts it
    // in the effect choice.
    public void ShowImported(ShareImportResult result)
    {
        if (result.Kind != SharedKind.Scene || result.Name is not { } name)
        {
            return;
        }

        _session.Read(new object(), api => api.GetProfilesAsync(), names =>
        {
            ShowProfiles(names, select: name, withActive: false);
        });
    }

    // Another settings app saved or deleted a scene, or this one did: the list and the cards
    // follow, the selection stays, and so do fields being typed in for the same scene. The boxes
    // of the devices of the open scene follow too, so the next click does not undo a change made
    // elsewhere. In the queue, so it reads the engine after the calls of this app that wait.
    public void Reload()
    {
        if (!_session.HasConnection)
        {
            return;
        }

        var open = _profiles.SelectedName;
        _session.PostOn(ProfilesReloadCall, async connection =>
        {
            var names = await connection.Api.GetProfilesAsync();
            var wholeHouse = await connection.Api.GetWholeHouseTimesAsync();
            var (devicesRead, deviceIds) = open is not null && names.Contains(open, ProfileNames.Comparer)
                ? await _session.TryCallAsync(connection, api => api.GetProfileDevicesAsync(open))
                : (false, null);
            _session.WhenCurrent(connection, () =>
            {
                ShowProfiles(names);
                ShowWholeHouseTimes(wholeHouse);
                if (devicesRead && _profiles.SelectedName == open)
                {
                    ShowSceneDevices(open!, deviceIds);
                }
            });
        });
    }

    // Asked again after every change of the lighting or of the scenes: any other change than a
    // scene ends the scene that was on.
    public async Task ShowActiveAsync()
    {
        if (_session.Current is not { } current)
        {
            return;
        }

        var question = ++_activeQuestions;
        try
        {
            var active = await current.Api.GetActiveProfileAsync();
            if (_session.IsCurrent(current) && question == _activeQuestions)
            {
                _profiles.ShowActive(active);
            }
        }
        catch (Exception exception)
        {
            EngineSession.OnReadFailed(exception);
        }
    }

    // In the queue behind the changes that still wait, so a profile saves what the user set last.
    // Each call has a key of its own, so two clicks are two calls. Afterwards the list shows the
    // profiles of the engine with this one selected, and then done runs, such as showing the
    // lighting of a loaded profile. When the engine refuses the call for a scene that is still
    // there, failed runs as well as the refusal is shown. isNew is for saving, where the scene need
    // not exist yet.
    private void SendProfileCall(string name, Func<IEngineApi, Task> call, string doneMessage, Action? done = null, bool isNew = false, Action? failed = null)
    {
        _session.PostOn(new object(), async connection =>
        {
            try
            {
                await call(connection.Api);
            }
            catch (Exception exception) when (!isNew && EngineRpc.IsRejectedByEngine(exception))
            {
                // Most likely the scene is gone, deleted elsewhere since the list showed it (a
                // second settings app, or by hand in profiles.json): say so, and show the list as
                // it is now. Otherwise the general message.
                var current = await connection.Api.GetProfilesAsync();
                if (current.Contains(name, ProfileNames.Comparer))
                {
                    if (failed is not null)
                    {
                        _session.WhenCurrent(connection, failed);
                    }

                    throw;
                }

                Trace.TraceWarning(exception.ToString());
                _session.WhenCurrent(connection, () =>
                {
                    ShowProfiles(current, withActive: false);
                    Message?.Invoke(this, UiText.Format(Strings.ProfileGone, name));
                });
                return;
            }

            var names = await connection.Api.GetProfilesAsync();
            _session.WhenCurrent(connection, () =>
            {
                ShowProfiles(names, select: name);
                Message?.Invoke(this, UiText.Format(doneMessage, name));
                done?.Invoke();
            });
        });
    }

    // Unless the user picked another profile, or the connection was made again, in the meantime.
    private async Task ShowProfileAutomationAsync(string name)
    {
        if (_session.Current is not { } connection)
        {
            return;
        }

        var (ok, automation) = await _session.TryCallAsync(connection, api => api.GetProfileAutomationAsync(name));
        if (ok && _session.IsCurrent(connection) && _profiles.SelectedName == name)
        {
            _profiles.Automation.Show(name, automation);
        }

        await ShowSceneDevicesAsync(name);
    }

    // The devices the engine saved for the scene, unless the user picked another scene in the
    // meantime or the connection was made again.
    private async Task ShowSceneDevicesAsync(string name)
    {
        if (_session.Current is not { } connection)
        {
            return;
        }

        var (ok, deviceIds) = await _session.TryCallAsync(connection, api => api.GetProfileDevicesAsync(name));
        if (ok && _session.IsCurrent(connection) && _profiles.SelectedName == name)
        {
            ShowSceneDevices(name, deviceIds);
        }
    }

    private void ShowSceneDevices(string name, IReadOnlyList<string>? deviceIds) =>
        _profiles.SceneDevices.Show(name, deviceIds, _wholeHouseTimes.OffScene is { } offScene && ProfileNames.Comparer.Equals(offScene, name));

    // The scenes of the engine on Scènes with select selected (Profiles.Show), and their cards.
    private void ShowProfiles(IReadOnlyList<string> names, string? select = null, bool withActive = true)
    {
        _profiles.Show(names, select);
        ShowCards(withActive);
    }

    // The cards of the scenes on Scènes load what each one looks like; withActive asks again which
    // one is on as well.
    private void ShowCards(bool withActive = true)
    {
        _ = ShowCardsAsync();
        if (withActive)
        {
            _ = ShowActiveAsync();
        }
    }

    // Only reads, so a scene that fails, such as one deleted from the tray menu meanwhile, keeps its
    // dark card and the user gets no message about a change (OnReadFailed); the other scenes still
    // get theirs.
    private async Task ShowCardsAsync()
    {
        if (_session.Current is not { } current)
        {
            return;
        }

        foreach (var name in _profiles.Names.ToArray())
        {
            try
            {
                var lighting = await current.Api.GetProfileLightingAsync(name);
                var automation = await current.Api.GetProfileAutomationAsync(name);
                if (!_session.IsCurrent(current))
                {
                    return;
                }

                _profiles.ShowCard(name, lighting, automation);
            }
            catch (Exception exception)
            {
                EngineSession.OnReadFailed(exception);
            }
        }
    }

    // "Uit om" and "Aan om" as the engine saved them. They are the times of "Alles uit": when that
    // scene is open on Scènes, its times there follow a change here. Whether the open scene has a
    // choice of devices follows which scene is "Alles uit".
    private void ShowWholeHouseTimes(WholeHouseTimes times)
    {
        var before = _wholeHouseTimes.Saved.Off;
        _wholeHouseTimes.Show(times);
        if (_profiles.SceneDevices.ProfileName is { } open)
        {
            _profiles.SceneDevices.ShowIsAllOff(times.OffScene is { } offScene && ProfileNames.Comparer.Equals(offScene, open));
        }

        if (times.OffScene is { } scene && _profiles.SelectedName is { } selected && ProfileNames.Comparer.Equals(scene, selected) && !before.SequenceEqual(times.Off))
        {
            _ = ShowProfileAutomationAsync(selected);
        }
    }

    // In the queue behind the changes that still wait; the newest times win. The engine answers
    // with ProfilesChanged too, which shows them again.
    private void SendWholeHouseTimes(WholeHouseTimes times)
    {
        _session.PostOn(WholeHouseTimesCall, async connection =>
        {
            await connection.Api.SetWholeHouseTimesAsync(times);
            var saved = await connection.Api.GetWholeHouseTimesAsync();
            _session.WhenCurrent(connection, () =>
            {
                ShowWholeHouseTimes(saved);
                Message?.Invoke(this, Strings.WholeHouseTimesSaved);
            });
        });
    }
}
