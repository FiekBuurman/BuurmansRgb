using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Uit om" and "Aan om" in the block Hele huis: a change counts at once, as the switches of the app.
public sealed class WholeHouseTimesViewModelTests
{
    private static readonly LoadTime ElevenPm = new(new TimeOnly(23, 0));
    private static readonly LoadTime SevenAm = new(new TimeOnly(7, 0));
    private static readonly LoadTime HalfPastSixAm = new(new TimeOnly(6, 30));

    private readonly WholeHouseTimesViewModel _times = new();
    private readonly List<WholeHouseTimes> _sent = [];

    public WholeHouseTimesViewModelTests() => _times.ApplyRequested += (_, times) => _sent.Add(times);

    [Fact]
    public void BeforeTheEngineAnswers_SendsNothing()
    {
        _times.Off.IsOn = true;

        Assert.False(_times.IsAvailable);
        Assert.Empty(_sent);
    }

    [Fact]
    public void Show_FillsInTheRows()
    {
        _times.Show(new WholeHouseTimes([new LoadTime(new TimeOnly(22, 30), WeekDays.Workdays)]));

        Assert.True(_times.Off.IsOn);
        Assert.Equal("22:30", _times.Off.Text);
        Assert.Equal([true, true, true, true, true, false, false], _times.Off.Days.Select(day => day.IsChecked));
        Assert.False(_times.On.IsOn);
        Assert.Empty(_sent);
    }

    // Truus switches it on: 23:00 every day, without typing.
    [Fact]
    public void SwitchOn_SendsTheUsualTimeEveryDay()
    {
        _times.Show(WholeHouseTimes.None);

        _times.Off.IsOn = true;
        _times.On.IsOn = true;

        Assert.Equal([new WholeHouseTimes([ElevenPm]), new WholeHouseTimes([ElevenPm], [SevenAm])], _sent);
    }

    [Fact]
    public void ADayOrATime_IsSentAtOnce()
    {
        _times.Show(new WholeHouseTimes([ElevenPm]));

        _times.Off.Days[6].IsChecked = false;
        _times.Off.Text = "22.30";

        Assert.Equal(
            [
                new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0), WeekDays.Every & ~WeekDays.Sunday)]),
                new WholeHouseTimes([new LoadTime(new TimeOnly(22, 30), WeekDays.Every & ~WeekDays.Sunday)]),
            ],
            _sent);
    }

    [Fact]
    public void WrongTimeOrNoDay_SaysWhatIsWrong_AndSendsNothing()
    {
        _times.Show(new WholeHouseTimes([ElevenPm], [SevenAm]));

        _times.On.Text = "7 uur";
        Assert.Equal("Vul bij Aan om een tijd in zoals 23:00.", _times.Error);

        _times.On.Text = "07:15";
        Assert.Null(_times.Error);
        foreach (var day in _times.Off.Days)
        {
            day.IsChecked = false;
        }

        Assert.Equal("Kies bij Uit om minstens één dag.", _times.Error);
        Assert.Equal(new WholeHouseTimes([ElevenPm], [new LoadTime(new TimeOnly(7, 15))]), _sent[0]);
        Assert.All(_sent, times => Assert.NotEqual(WeekDays.None, times.Off[0].Days));
    }

    // Aan om at the time of Uit om on a day of both would never happen: said, not sent.
    [Fact]
    public void UitOmAndAanOmTogether_SaysSo_AndSendsNothing()
    {
        _times.Show(new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0), WeekDays.Workdays)], [SevenAm]));

        _times.On.Text = "23:00";
        Assert.Equal("Uit om en Aan om vallen samen. Kies een andere tijd of andere dagen.", _times.Error);
        Assert.Empty(_sent);

        foreach (var day in _times.On.Days.Take(5))
        {
            day.IsChecked = false;
        }

        Assert.Null(_times.Error);
        Assert.Equal(new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0), WeekDays.Workdays)], [new LoadTime(new TimeOnly(23, 0), WeekDays.Weekend)]), Assert.Single(_sent));
    }

    // Saved like that, for example through Scènes: the note is there at once.
    [Fact]
    public void Show_UitOmAndAanOmTogether_SaysSo()
    {
        _times.Show(new WholeHouseTimes([ElevenPm], [ElevenPm]));

        Assert.True(_times.HasError);
    }

    // Switched on with a time that does not read, the field gets the usual time.
    [Fact]
    public void SwitchOn_WithAWrongTimeInTheField_TakesTheUsualTime()
    {
        _times.Show(WholeHouseTimes.None);
        _times.On.Text = "nu";

        _times.On.IsOn = true;

        Assert.Equal("07:00", _times.On.Text);
        Assert.Equal(new WholeHouseTimes(On: [SevenAm]), _sent[^1]);
    }

    // Alles uit with more times on Scènes: the row shows the first and cannot be changed, so no time
    // jumps or goes; Aan om still can, and keeps all of them.
    [Fact]
    public void SceneWithMoreTimes_TheRowShowsTheFirst_AndCannotBeChanged()
    {
        _times.Show(new WholeHouseTimes([HalfPastSixAm, ElevenPm], OffScene: "Alles uit"));

        Assert.False(_times.Off.IsEditable);
        Assert.Equal("06:30", _times.Off.Text);
        Assert.True(_times.On.IsEditable);
        Assert.Equal("Uit om zet de scène 'Alles uit' aan. Die heeft nog 1 andere tijd, dus Uit om verander je op Scènes.", _times.SceneNote);

        _times.Off.IsOn = false;
        Assert.Empty(_sent);

        _times.On.IsOn = true;
        Assert.Equal([new WholeHouseTimes([HalfPastSixAm, ElevenPm], [SevenAm], OffScene: "Alles uit")], _sent);
    }

    [Fact]
    public void SceneWithMoreTimes_SaysHowMany_AndOneTimeCanBeChangedAgain()
    {
        _times.Show(new WholeHouseTimes([SevenAm, new LoadTime(new TimeOnly(22, 0)), ElevenPm], OffScene: "Alles uit"));
        Assert.Equal("Uit om zet de scène 'Alles uit' aan. Die heeft nog 2 andere tijden, dus Uit om verander je op Scènes.", _times.SceneNote);

        _times.Show(new WholeHouseTimes([ElevenPm], OffScene: "Alles uit"));
        Assert.True(_times.Off.IsEditable);
        _times.Off.IsOn = false;
        Assert.Equal([new WholeHouseTimes(OffScene: "Alles uit")], _sent);
    }

    [Fact]
    public void SceneNote_NamesTheScene_OnlyWhileThereIsOne()
    {
        _times.Show(new WholeHouseTimes([ElevenPm], OffScene: "Bedtijd"));
        Assert.Equal("Uit om zet de scène 'Bedtijd' aan. Op Scènes zie je dezelfde tijd bij die scène.", _times.SceneNote);
        Assert.True(_times.HasSceneNote);

        _times.Show(new WholeHouseTimes([ElevenPm]));
        Assert.False(_times.HasSceneNote);
    }

    // With Uit om off, there is nothing to say about the scene.
    [Fact]
    public void SceneNote_OnlyWhileUitOmHasATime()
    {
        _times.Show(new WholeHouseTimes(On: [SevenAm], OffScene: "Alles uit"));

        Assert.False(_times.HasSceneNote);
    }

    [Fact]
    public void Summary_SaysTheTimesThatAreOn()
    {
        _times.Show(WholeHouseTimes.None);
        Assert.Null(_times.Summary);
        Assert.False(_times.HasTimes);

        _times.Show(new WholeHouseTimes([ElevenPm]));
        Assert.Equal("uit om 23:00 (elke dag)", _times.Summary);

        _times.Show(new WholeHouseTimes([ElevenPm], [new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays)]));
        Assert.Equal("uit om 23:00 (elke dag) en aan om 07:00 (werkdagen)", _times.Summary);
        Assert.True(_times.HasTimes);
    }

    // A scene "Alles uit" with more times: the first, and how many more.
    [Fact]
    public void Summary_WithMoreTimes_SaysHowManyMore()
    {
        var midnight = new LoadTime(new TimeOnly(0, 30));
        var oneAm = new LoadTime(new TimeOnly(1, 0));

        _times.Show(new WholeHouseTimes([ElevenPm, midnight], OffScene: "Alles uit"));
        Assert.Equal("uit om 23:00 (elke dag) en nog 1 tijd", _times.Summary);

        _times.Show(new WholeHouseTimes([ElevenPm, midnight, oneAm], OffScene: "Alles uit"));
        Assert.Equal("uit om 23:00 (elke dag) en nog 2 tijden", _times.Summary);

        _times.Show(new WholeHouseTimes([ElevenPm, midnight], [SevenAm], "Alles uit"));
        Assert.Equal("uit om 23:00 (elke dag) en nog 1 tijd, en aan om 07:00 (elke dag)", _times.Summary);
    }

    [Fact]
    public void Rows_HaveNamesForAScreenReader()
    {
        Assert.Equal(Strings.WholeHouseOff, _times.Off.Label);
        Assert.Equal("Tijd voor Uit om", _times.Off.TimeAccessibleName);
        Assert.Equal("Dagen voor Aan om", _times.On.DaysAccessibleName);
        Assert.Equal("maandag", _times.Off.Days[0].Name);
    }

    [Fact]
    public void Clear_SendsNothingUntilShownAgain()
    {
        _times.Show(WholeHouseTimes.None);
        _times.Clear();

        _times.Off.IsOn = true;

        Assert.Empty(_sent);
    }
}
