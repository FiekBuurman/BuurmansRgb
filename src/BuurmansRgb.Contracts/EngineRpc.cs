using System.Diagnostics;
using System.IO.Pipes;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts;

// Both ends of the pipe are set up here, so the engine and the UI always agree on the wire format.
public static class EngineRpc
{
    // Pipe names are machine-wide, but the engine runs once per Windows session (its mutex is
    // Local\). The session id keeps the engines of two logged-in users apart.
    public static string PipeName { get; } = $"BuurmansRgb.Engine.{CurrentSessionId()}";

    // CurrentUserOnly: only processes of this Windows user can connect.
    public const PipeOptions SharedPipeOptions = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

    public static NamedPipeServerStream CreateServerStream(string pipeName) =>
        new(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, SharedPipeOptions);

    public static JsonRpc StartServer(Stream stream, IEngineApi api)
    {
        var rpc = new JsonRpc(CreateMessageHandler(stream));
        // The generic overload exposes only IEngineApi, not every public member of the target.
        rpc.AddLocalRpcTarget<IEngineApi>(api, new JsonRpcTargetOptions { NotifyClientOfEvents = true });
        rpc.StartListening();
        return rpc;
    }

    // True when the engine answered a call with an error, such as invalid input; false when the
    // call failed for another reason, such as a lost connection. Keeps the transport library out
    // of the UI.
    public static bool IsRejectedByEngine(Exception exception) => exception is RemoteInvocationException;

    private static int CurrentSessionId()
    {
        using var process = Process.GetCurrentProcess();
        return process.SessionId;
    }

    internal static JsonRpc CreateClient(Stream stream) => new(CreateMessageHandler(stream));

    private static HeaderDelimitedMessageHandler CreateMessageHandler(Stream stream)
    {
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new RgbColorJsonConverter());
        return new HeaderDelimitedMessageHandler(stream, formatter);
    }
}
