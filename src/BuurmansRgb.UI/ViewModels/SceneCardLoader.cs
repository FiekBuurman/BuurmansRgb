using System.Diagnostics;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Shows on the scene cards what each scene looks like, when it loads by itself and which one is on.
// connection gives the current connection, or null. Used on the UI thread.
internal sealed class SceneCardLoader(ProfilesViewModel profiles, Func<IEngineConnection?> connection)
{
    // Counts the questions for the scene that is on, so an answer that a newer question overtook
    // does not bring back an older scene.
    private int _activeQuestions;

    // Only reads, so a scene that fails, such as one deleted from the tray menu meanwhile, keeps its
    // dark card and the user gets no message about a change; the other scenes still get theirs.
    public async Task ShowAsync()
    {
        if (connection() is not { } current)
        {
            return;
        }

        foreach (var name in profiles.Names.ToArray())
        {
            try
            {
                var lighting = await current.Api.GetProfileLightingAsync(name);
                var automation = await current.Api.GetProfileAutomationAsync(name);
                if (!ReferenceEquals(current, connection()))
                {
                    return;
                }

                profiles.ShowCard(name, lighting, automation);
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }
    }

    // Asked again after every change of the lighting or of the scenes: any other change than a
    // scene ends the scene that was on.
    public async Task ShowActiveAsync()
    {
        if (connection() is not { } current)
        {
            return;
        }

        var question = ++_activeQuestions;
        try
        {
            var active = await current.Api.GetActiveProfileAsync();
            if (ReferenceEquals(current, connection()) && question == _activeQuestions)
            {
                profiles.ShowActive(active);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
