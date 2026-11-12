using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineCommandTests
{
    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--first-run" }, null)]
    [InlineData(new[] { "--all-on" }, "--all-on")]
    [InlineData(new[] { "--ALL-OFF" }, "--all-off")]
    [InlineData(new[] { "--scene", "Gamen" }, "--scene 'Gamen'")]
    [InlineData(new[] { "--Scene", "Film kijken", "--first-run" }, "--scene 'Film kijken'")]
    [InlineData(new[] { "--scene" }, "--scene ''")]
    [InlineData(new[] { "--all-off", "--scene", "Gamen" }, "--all-off")]
    [InlineData(new[] { "--scene-later", "Gamen" }, null)]
    public void Parse_FindsTheFirstCommand(string[] args, string? command)
    {
        Assert.Equal(command, EngineOptions.Parse(args).Command?.ToString());
    }

    [Theory]
    [InlineData((int)EngineCommandKind.Scene, "Café: 's avonds")]
    [InlineData((int)EngineCommandKind.Scene, "")]
    [InlineData((int)EngineCommandKind.AllOn, "")]
    [InlineData((int)EngineCommandKind.AllOff, "")]
    public void FromMessage_ReadsWhatToMessageWrote(int kind, string scene)
    {
        var command = new EngineCommand((EngineCommandKind)kind, scene);

        Assert.Equal(command, EngineCommand.FromMessage(command.ToMessage()));
    }

    // From a newer engine, or not from an engine at all.
    [Theory]
    [InlineData("")]
    [InlineData("all-on-later")]
    [InlineData("Scene:Gamen")]
    [InlineData("pause")]
    public void FromMessage_Unknown_IsNull(string message)
    {
        Assert.Null(EngineCommand.FromMessage(message));
    }

    // The longest scene name in UTF-8 still fits in what a second start can send.
    [Fact]
    public void ToMessage_LongestSceneName_Fits()
    {
        var command = new EngineCommand(EngineCommandKind.Scene, new string('é', ProfileNames.MaxLength));

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(command.ToMessage()) <= SingleInstance.MaxCommandBytes);
    }

    [Theory]
    [InlineData("Gamen")]
    [InlineData("gamen")]
    [InlineData("  GAMEN ")]
    public void CheckBeforeStart_KnownScene_WithoutRegardToCaseOrSpaces_IsDone(string scene)
    {
        var store = new InMemoryProfileStore { Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))] };

        Assert.Equal(EngineCommandResult.Done, new EngineCommand(EngineCommandKind.Scene, scene).CheckBeforeStart(store));
    }

    [Fact]
    public void CheckBeforeStart_UnknownScene_IsUnknownScene_WithALineInTheLog()
    {
        var store = new InMemoryProfileStore { Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))] };
        using var trace = new TraceCapture();

        var result = new EngineCommand(EngineCommandKind.Scene, "Gamen2").CheckBeforeStart(store);

        Assert.Equal(EngineCommandResult.UnknownScene, result);
        Assert.Contains(trace.Messages, message => message.Contains("there is no scene 'Gamen2'", StringComparison.Ordinal));
    }

    // The engine starts and reports it, as without a command.
    [Fact]
    public void CheckBeforeStart_FileUnreadable_LeavesItToTheEngine()
    {
        var store = new InMemoryProfileStore { LoadException = new UnauthorizedAccessException() };

        Assert.Equal(EngineCommandResult.Done, new EngineCommand(EngineCommandKind.Scene, "Gamen").CheckBeforeStart(store));
    }

    [Fact]
    public void CheckBeforeStart_AllOnOrOff_DoesNotReadTheScenes()
    {
        var store = new InMemoryProfileStore();

        Assert.Equal(EngineCommandResult.Done, EngineCommand.AllOn.CheckBeforeStart(store));
        Assert.Equal(EngineCommandResult.Done, EngineCommand.AllOff.CheckBeforeStart(store));
        Assert.Equal(0, store.LoadCount);
    }

    // The exit codes the guide lists.
    [Theory]
    [InlineData((int)EngineCommandResult.Done, 0)]
    [InlineData((int)EngineCommandResult.Failed, 1)]
    [InlineData((int)EngineCommandResult.UnknownScene, 2)]
    [InlineData((int)EngineCommandResult.NotStarted, 3)]
    [InlineData((int)EngineCommandResult.NoAnswer, 4)]
    public void Result_IsTheExitCode(int result, int exitCode)
    {
        Assert.Equal(exitCode, result);
    }

    // Through the same signal as in Program, with a name of its own, so no engine that runs on
    // this pc hears it.
    [Fact]
    public async Task Send_EngineAnswers_GivesItsAnswer()
    {
        var name = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
        using var engine = SingleInstance.Acquire(name);
        string? received = null;
        using var listening = engine.ListenForCommand(message =>
        {
            received = message;
            return (int)EngineCommandResult.UnknownScene;
        });

        var result = await Task.Run(() => new EngineCommand(EngineCommandKind.Scene, "Nacht").Send(name), Ct);

        Assert.Equal(EngineCommandResult.UnknownScene, result);
        Assert.Equal("scene:Nacht", received);
    }

    [Fact]
    public void Send_NoEngineListens_IsNoAnswer()
    {
        Assert.Equal(EngineCommandResult.NoAnswer, EngineCommand.AllOn.Send($"BuurmansRgb.Tests.{Guid.NewGuid():N}"));
    }
}
