using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelProfileTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsTheProfilesOfTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));

        Assert.Equal(["Gamen", "Nacht"], viewModel.Profiles.Names);
        Assert.Null(viewModel.Profiles.SelectedName);
    }

    [Fact]
    public async Task Save_SendsAfterTheWaitingChangesAndSelectsTheNewProfile()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        viewModel.Brightness = 40;
        viewModel.Profiles.Name = "Gamen";

        viewModel.Profiles.SaveCommand.Execute(null);

        Assert.Equal(["brightness all 0.40", "profile save Gamen"], Api.Calls);
        Assert.Equal(["Gamen", "Werk"], viewModel.Profiles.Names);
        Assert.Equal("Gamen", viewModel.Profiles.SelectedName);
        Assert.Equal("Scène 'Gamen' opgeslagen.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Load_ShowsTheLightingOfTheProfile()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Nacht");
            api.ProfileLighting["Nacht"] = new LightingInfo(Blue, 0.1, EffectIds.Rainbow, 0.5);
        });
        viewModel.Profiles.SelectedName = "Nacht";

        viewModel.Profiles.LoadCommand.Execute(null);

        Assert.Equal(["profile load Nacht"], Api.Calls);
        Assert.Equal(10, viewModel.Brightness);
        Assert.Equal(EffectIds.Rainbow, viewModel.SelectedEffect?.Id);
        Assert.Equal(50, viewModel.Speed);
        Assert.Equal("Scène 'Nacht' staat aan.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Delete_RemovesTheProfileFromTheList()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.SelectedName = "Gamen";

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.Equal(["profile delete Gamen"], Api.Calls);
        Assert.Equal(["Nacht"], viewModel.Profiles.Names);
        Assert.Equal("Scène 'Gamen' verwijderd.", viewModel.StatusMessage);
    }

    // Deleted elsewhere since the list showed it, such as in a second settings app.
    [Fact]
    public async Task Delete_SceneThatIsGone_SaysSoAndShowsTheListOfTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.SelectedName = "Gamen";
        Api.Profiles.Remove("Gamen");
        Api.SetException = new RemoteInvocationException("There is no profile 'Gamen'.", 0, errorData: null);

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.Equal(["Nacht"], viewModel.Profiles.Names);
        Assert.Equal("Scène 'Gamen' bestaat niet meer.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ChangeRejected_GoesAfterTheNextChangeThatWorks()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        Api.SetException = new RemoteInvocationException("Unknown device.", 0, errorData: null);
        viewModel.Brightness = 10;
        Assert.Equal("De engine kon de wijziging niet verwerken.", viewModel.StatusMessage);

        Api.SetException = null;
        viewModel.Brightness = 20;

        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task Save_RejectedByTheEngine_ShowsTheErrorAndKeepsTheList()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        Api.SetException = new RemoteInvocationException("Disk full", 0, errorData: null);
        viewModel.Profiles.Name = "Gamen";

        viewModel.Profiles.SaveCommand.Execute(null);

        Assert.Equal(["Werk"], viewModel.Profiles.Names);
        Assert.Equal("De engine kon de wijziging niet verwerken.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SelectProfile_ShowsWhenItLoadsByItself()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Gamen", "Nacht"]);
            api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe");
        });

        viewModel.Profiles.SelectedName = "Nacht";

        Assert.Equal("Nacht", viewModel.Profiles.Automation.ProfileName);
        Assert.Equal("23:00", viewModel.Profiles.Automation.Times[0].Text);
        Assert.Equal("eldenring.exe", viewModel.Profiles.Automation.ProgramText);
    }

    [Fact]
    public async Task ApplyAutomation_SendsItAndShowsWhatTheEngineSaved()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Gamen"));
        viewModel.Profiles.SelectedName = "Gamen";

        viewModel.Profiles.Automation.ProgramText = "eldenring";
        viewModel.Profiles.Automation.ApplyCommand.Execute(null);

        Assert.Equal(["profile automation Gamen - eldenring.exe"], Api.Calls);
        Assert.Equal("eldenring.exe", viewModel.Profiles.Automation.ProgramText);
        Assert.Equal("Vanzelf aanzetten: aan", viewModel.Profiles.Automation.Header);
        Assert.False(viewModel.Profiles.Automation.ApplyCommand.CanExecute(null));
        Assert.Equal("Vanzelf aanzetten van 'Gamen' opgeslagen.", viewModel.StatusMessage);
    }

    // A time typed without a colon goes out as a time, and the field shows it as one after saving.
    [Fact]
    public async Task ApplyAutomation_TimeWithoutAColon_ShowsItWithOneAfterSaving()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Avond"));
        viewModel.Profiles.SelectedName = "Avond";

        viewModel.Profiles.Automation.Times[0].Text = "930";
        viewModel.Profiles.Automation.ApplyCommand.Execute(null);

        Assert.Equal(["profile automation Avond 09:30 -"], Api.Calls);
        Assert.Equal("09:30", viewModel.Profiles.Automation.Times[0].Text);
    }

    [Fact]
    public async Task ApplyAutomation_MoreTimes_SendsThemAndTheCardSaysSo()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Ochtend");
            api.ProfileAutomations["Ochtend"] = new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Weekend);
        });
        viewModel.Profiles.SelectedName = "Ochtend";

        viewModel.Profiles.Automation.AddTimeCommand.Execute(null);
        viewModel.Profiles.Automation.Times[1].Text = "07:00";
        viewModel.Profiles.Automation.ApplyCommand.Execute(null);
        // The engine tells every settings app, this one too.
        Api.RaiseProfilesChanged();

        Assert.Equal(["profile automation Ochtend 07:00,09:00 -"], Api.Calls);
        Assert.Equal(["07:00", "09:00"], viewModel.Profiles.Automation.Times.Select(row => row.Text));
        Assert.Equal("Elke dag om 07:00 en nog 1", Assert.Single(viewModel.Profiles.Cards).Description);
    }

    [Fact]
    public async Task DeleteProfile_EmptiesItsAutomation()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Nacht");
            api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0));
        });
        viewModel.Profiles.SelectedName = "Nacht";

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.False(viewModel.Profiles.Automation.HasProfile);
        Assert.Equal("", viewModel.Profiles.Automation.Times[0].Text);
    }

    // "Bijwerken met wat nu brandt" saves the lighting as it is now under the scene, once the user
    // said yes, and says so in its own words.
    [Fact]
    public async Task Update_SavesUnderTheSceneAfterTheQuestion()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.ConfirmUpdate = _ => true;
        viewModel.Profiles.SelectedName = "Nacht";

        viewModel.Profiles.UpdateCommand.Execute(null);

        Assert.Equal(["profile save Nacht"], Api.Calls);
        Assert.Equal("Nacht", viewModel.Profiles.SelectedName);
        Assert.Equal("Scène 'Nacht' bijgewerkt.", viewModel.StatusMessage);
    }

    // The boxes have the names of the devices, checked for those of the scene; a device of the scene
    // that is not there now is named by how many there are, and stays in the scene.
    [Fact]
    public async Task SelectProfile_ShowsItsDevices()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Gamen", "Lezen"]);
            api.ProfileDevices["Lezen"] = ["fan", "headset"];
        });

        viewModel.Profiles.SelectedName = "Gamen";
        var devices = viewModel.Profiles.SceneDevices;
        Assert.True(devices.AllDevices);
        Assert.False(devices.CanPick);
        Assert.Equal([("LED strip", true), ("Fan", true)], devices.Options.Select(option => (option.Name, option.IsChecked)));

        viewModel.Profiles.SelectedName = "Lezen";
        Assert.False(devices.AllDevices);
        Assert.True(devices.CanPick);
        Assert.Equal([("LED strip", false), ("Fan", true)], devices.Options.Select(option => (option.Name, option.IsChecked)));
        Assert.Equal("Doet ook mee: 1 apparaat dat er nu niet is.", devices.AwayNote);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task CheckDevices_SendsThemAtOnceAndKeepsTheOnesThatAreAway()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Lezen");
            api.ProfileDevices["Lezen"] = ["fan", "headset"];
        });
        viewModel.Profiles.SelectedName = "Lezen";
        var devices = viewModel.Profiles.SceneDevices;

        devices.Options[0].IsChecked = true;
        devices.Options[1].IsChecked = false;
        devices.AllDevices = true;

        Assert.Equal(["profile devices Lezen strip,fan,headset", "profile devices Lezen strip,headset", "profile devices Lezen all"], Api.Calls);
        Assert.Equal("Apparaten van 'Lezen' opgeslagen.", viewModel.StatusMessage);
        Assert.False(Api.ProfileDevices.ContainsKey("Lezen"));
    }

    // Such as profiles.json that cannot be written: the boxes show again what the engine has, so the
    // next change is sent too.
    [Fact]
    public async Task CheckDevices_RejectedByTheEngine_ShowsWhatTheEngineHas()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Lezen");
            api.ProfileDevices["Lezen"] = ["fan"];
        });
        viewModel.Profiles.SelectedName = "Lezen";
        var devices = viewModel.Profiles.SceneDevices;
        Api.SetException = new RemoteInvocationException("Disk full", 0, errorData: null);

        devices.AllDevices = true;

        Assert.Equal("De engine kon de wijziging niet verwerken.", viewModel.StatusMessage);
        Assert.False(devices.AllDevices);
        Assert.Equal([("LED strip", false), ("Fan", true)], devices.Options.Select(option => (option.Name, option.IsChecked)));

        Api.SetException = null;
        devices.AllDevices = true;

        Assert.Equal(["profile devices Lezen all"], Api.Calls);
        Assert.False(Api.ProfileDevices.ContainsKey("Lezen"));
    }

    // Another settings app, or restoring the settings, changed the devices of the open scene: the
    // boxes follow, so the next click sends the change on top of it and does not undo it.
    [Fact]
    public async Task DevicesChangedElsewhere_TheOpenSceneFollows()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Lezen");
            api.ProfileDevices["Lezen"] = ["fan"];
        });
        viewModel.Profiles.SelectedName = "Lezen";
        var devices = viewModel.Profiles.SceneDevices;

        Api.ProfileDevices.Remove("Lezen");
        Api.RaiseProfilesChanged();
        Assert.True(devices.AllDevices);
        Assert.All(devices.Options, option => Assert.True(option.IsChecked));

        Api.ProfileDevices["Lezen"] = ["strip", "fan"];
        Api.RaiseProfilesChanged();
        devices.Options[0].IsChecked = false;

        Assert.Equal(["profile devices Lezen fan"], Api.Calls);
        Assert.Equal("Lezen", viewModel.Profiles.SelectedName);
    }

    // "Alle apparaten" off starts with every device there is now checked; then a scene of no device
    // is not saved, as it would do nothing, and the message says why until a box is on again.
    [Fact]
    public async Task UncheckEveryDevice_SaysSoAndSendsNothing()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Lezen"));
        viewModel.Profiles.SelectedName = "Lezen";
        var devices = viewModel.Profiles.SceneDevices;

        devices.AllDevices = false;
        Assert.All(devices.Options, option => Assert.True(option.IsChecked));
        devices.Options[0].IsChecked = false;
        devices.Options[1].IsChecked = false;

        Assert.Equal(["profile devices Lezen strip,fan", "profile devices Lezen fan"], Api.Calls);
        Assert.Equal("Kies minstens één apparaat; een scène zonder apparaten doet niets.", devices.Error);
        Assert.Equal(["fan"], Api.ProfileDevices["Lezen"]);

        devices.Options[0].IsChecked = true;
        Assert.Null(devices.Error);
        Assert.Equal("profile devices Lezen strip", Api.Calls[^1]);
    }

    // "Alles uit" switches every device off for "Uit om", so it has no choice, and says why.
    [Fact]
    public async Task SelectAllOff_HasNoChoiceOfDevices()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Alles uit", "Lezen"]);
            api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Alles uit");
        });

        viewModel.Profiles.SelectedName = "Alles uit";

        var devices = viewModel.Profiles.SceneDevices;
        Assert.True(devices.IsAllOff);
        Assert.False(devices.CanChoose);
        Assert.Contains("'Alles uit'", devices.AllOffNote, StringComparison.Ordinal);
        devices.AllDevices = false;
        Assert.Empty(Api.Calls);

        viewModel.Profiles.SelectedName = "Lezen";
        Assert.False(devices.IsAllOff);
        Assert.True(devices.CanChoose);
        Assert.Null(devices.AllOffNote);
    }

    // Such as after restoring the settings: the open scene gets or loses its choice of devices.
    [Fact]
    public async Task AllOffChanges_TheOpenSceneFollows()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Alles uit", "Nacht"]);
            api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Alles uit");
        });
        viewModel.Profiles.SelectedName = "Nacht";
        var devices = viewModel.Profiles.SceneDevices;
        Assert.True(devices.CanChoose);

        Api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Nacht");
        Api.RaiseProfilesChanged();
        Assert.True(devices.IsAllOff);
        Assert.False(devices.CanChoose);

        Api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Alles uit");
        Api.RaiseProfilesChanged();
        Assert.False(devices.IsAllOff);
        Assert.True(devices.CanChoose);
        Assert.Empty(Api.Calls);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed];
            arrange(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
