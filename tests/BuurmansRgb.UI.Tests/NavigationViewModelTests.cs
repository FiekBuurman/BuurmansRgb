using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class NavigationViewModelTests
{
    [Fact]
    public void Items_FollowTheDesignWithTheSettingsAtTheBottom()
    {
        Assert.Equal(["Lampen", "Kleur", "Scènes", "Instellingen"], NavigationViewModel.Items.Select(item => item.Label));
        Assert.Equal([AppPage.Settings], NavigationViewModel.Items.Where(item => item.IsAtBottom).Select(item => item.Page));
        Assert.Equal("Scènes", NavigationViewModel.Items[2].ToString());
    }

    [Fact]
    public void New_ShowsTheLamps()
    {
        var navigation = new NavigationViewModel();

        Assert.Equal(AppPage.Lamps, navigation.CurrentPage);
        Assert.Equal(AppPage.Lamps, navigation.SelectedItem?.Page);
    }

    [Fact]
    public void GoTo_SelectsThePageAndShowsIt()
    {
        var navigation = new NavigationViewModel();
        List<string?> changes = [];
        navigation.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        navigation.GoToCommand.Execute(AppPage.Settings);

        Assert.Equal(AppPage.Settings, navigation.CurrentPage);
        Assert.Equal(AppPage.Settings, navigation.SelectedItem?.Page);
        Assert.Contains(nameof(NavigationViewModel.CurrentPage), changes);
    }

    [Fact]
    public void SelectedItem_SetToNothing_KeepsThePage()
    {
        var navigation = new NavigationViewModel();
        navigation.GoTo(AppPage.Scenes);

        navigation.SelectedItem = null;

        Assert.Equal(AppPage.Scenes, navigation.CurrentPage);
        Assert.Equal(AppPage.Scenes, navigation.SelectedItem?.Page);
    }

    [Fact]
    public void SelectedItem_SetToNothingOnTheUiThread_SelectsThePageAgainAfterwards()
    {
        var context = new QueuedSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        NavigationViewModel navigation;
        try
        {
            navigation = new NavigationViewModel();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        navigation.GoTo(AppPage.Color);

        navigation.SelectedItem = null;
        Assert.Null(navigation.SelectedItem);
        context.RunPending();

        Assert.Equal(AppPage.Color, navigation.SelectedItem?.Page);
        Assert.Equal(AppPage.Color, navigation.CurrentPage);
    }
}
