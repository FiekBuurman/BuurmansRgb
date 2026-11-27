using System.Reflection;

namespace BuurmansRgb.Engine.Tests;

// How many handlers listen to the events of a part: a field-like event keeps them in a field with
// its own name, also in a base class. For a test that a class stops listening to every part it
// listened to: count before it starts, and again after Dispose.
internal static class EventListeners
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static int Count(object source)
    {
        var count = 0;
        for (var type = source.GetType(); type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(Fields))
            {
                if (type.GetEvent(field.Name, Fields) is not null && field.GetValue(source) is Delegate handlers)
                {
                    count += handlers.GetInvocationList().Length;
                }
            }
        }

        return count;
    }

    // The count of each part, in the order given.
    public static int[] CountEach(IEnumerable<object> sources) => [.. sources.Select(Count)];
}
