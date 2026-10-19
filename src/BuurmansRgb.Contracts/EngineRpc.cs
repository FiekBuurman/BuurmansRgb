using System.IO.Pipes;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts;

// Both ends of the pipe are set up here, so the engine and the UI always agree on the wire format.
public static class EngineRpc
{
    public const string PipeName = "BuurmansRgb.Engine";

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

    internal static JsonRpc CreateClient(Stream stream) => new(CreateMessageHandler(stream));

    private static HeaderDelimitedMessageHandler CreateMessageHandler(Stream stream)
    {
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new RgbColorJsonConverter());
        return new HeaderDelimitedMessageHandler(stream, formatter);
    }
}
