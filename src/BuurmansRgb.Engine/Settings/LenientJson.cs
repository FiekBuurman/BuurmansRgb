using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace BuurmansRgb.Engine.Settings;

// Reading settings.json and profiles.json so that one value of the wrong type, such as
// "brightness": "high" or "on": "yes", is left out instead of the whole file: throwing would make
// the engine run on the defaults, and the next save would overwrite all the lighting in the file.
// The value left out takes its default, as if it were not in the file. A value inside a list becomes
// null, such as a scene that is not an object; where the list does not take null, the whole list is
// left out, as it would mean something else with one value less. A file that is not valid JSON, or
// not an object, still throws.
internal static class LenientJson
{
    // Each value left out reads the file once more; a file with more is too far off to guess at.
    private const int MaxLeftOut = 100;

    public static T? Deserialize<T>(byte[] json, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException exception) when (exception.Path is not null)
        {
            return DeserializeLeavingOut(json, typeInfo, exception);
        }
    }

    private static T? DeserializeLeavingOut<T>(byte[] json, JsonTypeInfo<T> typeInfo, JsonException first)
    {
        try
        {
            return LeaveOut(json, typeInfo, first);
        }
        catch (ArgumentException exception)
        {
            // A name twice in one object: the serializer takes the last one, but JsonNode refuses
            // the file. Which one the user meant is anyone's guess, so it goes aside as before.
            Trace.TraceWarning($"Settings file: {exception.Message}");
            ExceptionDispatchInfo.Throw(first);
            throw;
        }
    }

    private static T? LeaveOut<T>(byte[] json, JsonTypeInfo<T> typeInfo, JsonException first)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Not valid JSON: there is no single value to leave out.
            ExceptionDispatchInfo.Throw(first);
            throw;
        }

        var exception = first;
        var nulled = new HashSet<string>(StringComparer.Ordinal);
        for (var leftOut = 0; leftOut < MaxLeftOut; leftOut++)
        {
            if (!TryLeaveOut(root, exception.Path!, nulled))
            {
                break;
            }

            Trace.TraceWarning($"Settings file: the value at {exception.Path} is not of the right type; it is left out. {exception.Message}");
            try
            {
                return root.Deserialize(typeInfo);
            }
            catch (JsonException next) when (next.Path is not null)
            {
                exception = next;
            }
        }

        ExceptionDispatchInfo.Throw(exception);
        throw exception;
    }

    // Sets the list item that path ends in to null, the first time; otherwise removes the property
    // that path ends in, or that holds the list, from its object. False for the root itself, or for
    // a path that is not in the document.
    private static bool TryLeaveOut(JsonNode? root, string path, HashSet<string> nulled)
    {
        if (!TryParsePath(path, out var segments))
        {
            return false;
        }

        if (segments.Count > 0 && segments[^1] is int item
            && Find(root, segments[..^1]) is JsonArray items && item < items.Count && nulled.Add(path))
        {
            items[item] = null;
            return true;
        }

        while (segments.Count > 0 && segments[^1] is int)
        {
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        return Find(root, segments[..^1]) is JsonObject owner && owner.Remove((string)segments[^1]);
    }

    private static JsonNode? Find(JsonNode? root, List<object> segments)
    {
        var node = root;
        foreach (var segment in segments)
        {
            node = segment switch
            {
                string name => node is JsonObject parent ? parent[name] : null,
                int index => node is JsonArray list && index < list.Count ? list[index] : null,
                _ => null,
            };
        }

        return node;
    }

    // The path of a JsonException: "$", then ".name", "['name with \' or a dot']" or "[3]" per step.
    private static bool TryParsePath(string path, out List<object> segments)
    {
        segments = [];
        if (!path.StartsWith('$'))
        {
            return false;
        }

        var i = 1;
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                var end = path.IndexOfAny(['.', '['], i + 1);
                end = end < 0 ? path.Length : end;
                segments.Add(path[(i + 1)..end]);
                i = end;
            }
            else if (path.AsSpan(i).StartsWith("['"))
            {
                var name = new StringBuilder();
                i += 2;
                while (i < path.Length && !path.AsSpan(i).StartsWith("']"))
                {
                    if (path[i] == '\\' && i + 1 < path.Length && path[i + 1] == '\'')
                    {
                        i++;
                    }

                    name.Append(path[i]);
                    i++;
                }

                if (i >= path.Length)
                {
                    return false;
                }

                segments.Add(name.ToString());
                i += 2;
            }
            else if (path[i] == '[')
            {
                var end = path.IndexOf(']', i);
                if (end < 0 || !int.TryParse(path.AsSpan(i + 1, end - i - 1), out var index))
                {
                    return false;
                }

                segments.Add(index);
                i = end + 1;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}
