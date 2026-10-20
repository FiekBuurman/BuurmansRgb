using System.Diagnostics;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Shows on the scene cards what each scene looks like and when it loads by itself. connection gives
// the current connection, or null. Used on the UI thread.
internal sealed class SceneCardLoader(ProfilesViewModel profiles, Func<IEngineConnection?> connection)
{
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
}
