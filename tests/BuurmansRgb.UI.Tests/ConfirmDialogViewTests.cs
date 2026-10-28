using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The question before something is deleted, as a real window off screen beside the window of the
// test, its owner. Shown without ShowDialog, which would take the focus of the desktop; Ask only
// adds that.
[Collection(AppThemeCollection.Name)]
public sealed class ConfirmDialogViewTests
{
    private const string Question = "Scène 'Avond' verwijderen?";
    private const string Detail = "Apparaten en scènes met dit effect worden dan Statisch.";

    // The Dutch buttons of the app, Annuleren first and with the focus; a screen reader reads the
    // question as the name of the window and the detail as its help.
    [Fact]
    public void Dialog_ShowsTheQuestionWithDutchButtons()
    {
        Show(Detail, (owner, dialog) =>
        {
            var buttons = Buttons(dialog);

            Assert.Equal([Strings.Cancel, Strings.ConfirmDelete], buttons.Select(button => button.Content));
            Assert.Equal([Strings.Cancel, Strings.ConfirmDelete], buttons.Select(button => new ButtonAutomationPeer(button).GetName()));
            Assert.Same(buttons[0], FocusManager.GetFocusedElement(dialog));
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(dialog));

            var peer = UIElementAutomationPeer.CreatePeerForElement(dialog);
            Assert.Equal(Question, peer.GetName());
            Assert.Equal(Detail, peer.GetHelpText());
            Assert.Contains(Texts(dialog), text => text.IsVisible && text.Text == Question);
            Assert.Contains(Texts(dialog), text => text.IsVisible && text.Text == Detail);

            // Modal on the window that asks, centered on it and not on the taskbar.
            Assert.Same(owner, dialog.Owner);
            Assert.Equal(WindowStartupLocation.CenterOwner, dialog.WindowStartupLocation);
            Assert.False(dialog.ShowInTaskbar);

            // The title bar of the app: its icon, which a screen reader names, and the word mark.
            var icon = Assert.Single(AppTheme.Descendants<Image>(dialog));
            Assert.Equal(Strings.WindowTitle, new ImageAutomationPeer(icon).GetName());
            Assert.Contains(Texts(dialog), text => text.IsVisible && new TextBlockAutomationPeer(text).GetName() == "buurmansRgb");
        });
    }

    // The two questions of the app, with the name of what goes; only the own effect says what
    // then happens to the devices and scenes that have it.
    [Fact]
    public void ForScene_AsksWithTheNameOfTheScene()
    {
        Make(owner => ConfirmDialog.ForScene(owner, "Avond"), (owner, dialog) =>
        {
            Assert.Same(owner, dialog.Owner);
            Assert.Equal(UiText.Format(Strings.ProfileDeleteConfirm, "Avond"), dialog.Title);
            Assert.Contains("'Avond'", dialog.Title, StringComparison.Ordinal);
            Assert.Equal(string.Empty, AutomationProperties.GetHelpText(dialog));
        });
    }

    [Fact]
    public void ForCustomEffect_AsksWithTheNameAndWhatHappensToTheDevices()
    {
        Make(owner => ConfirmDialog.ForCustomEffect(owner, "Disco"), (owner, dialog) =>
        {
            Assert.Same(owner, dialog.Owner);
            Assert.Equal(UiText.Format(Strings.CustomEffectDeleteConfirm, "Disco"), dialog.Title);
            Assert.Contains("'Disco'", dialog.Title, StringComparison.Ordinal);
            Assert.Equal(Strings.CustomEffectDeleteConfirmDetail, AutomationProperties.GetHelpText(dialog));
        });
    }

    // SizeToContent measures with the frame of Windows, which the title bar of our own then
    // takes away; the window is as large as what is in it plus its border, without empty space
    // on the right and at the bottom.
    [Theory]
    [InlineData(Detail)]
    [InlineData(null)]
    public void Dialog_IsAsLargeAsItsContent(string? detail)
    {
        Show(detail, (_, dialog) =>
        {
            var content = (FrameworkElement)dialog.Content;
            var border = dialog.BorderThickness;

            Assert.Equal(content.ActualWidth + border.Left + border.Right, dialog.ActualWidth, 0.5);
            Assert.Equal(content.ActualHeight + border.Top + border.Bottom, dialog.ActualHeight, 0.5);
        });
    }

    // Measured again, the window still stands in the middle of its owner. Windows moves a window
    // off screen onto it, so here the owner stands on screen, for as long as the test lasts. Up
    // to a pixel, which Windows rounds to.
    [Fact]
    public void Dialog_StaysInTheMiddleOfItsOwner()
    {
        Show(Detail, (owner, dialog) =>
        {
            Assert.Equal(owner.Left + (owner.ActualWidth / 2), dialog.Left + (dialog.ActualWidth / 2), 1.0);
            Assert.Equal(owner.Top + (owner.ActualHeight / 2), dialog.Top + (dialog.ActualHeight / 2), 1.0);
        }, ownerOnScreen: true);
    }

    [Fact]
    public void Dialog_WithoutDetail_ShowsOnlyTheQuestion()
    {
        Show(null, (_, dialog) =>
        {
            var question = Texts(dialog).Single(text => text.Text == Question);
            var under = (Panel)question.Parent;

            Assert.Equal([question], under.Children.OfType<TextBlock>().Where(text => text.IsVisible));
            Assert.Equal(string.Empty, UIElementAutomationPeer.CreatePeerForElement(dialog).GetHelpText());
        });
    }

    // Enter does the button that has the focus, so Annuleren keeps everything.
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Enter_DoesTheFocusedButton(int index, bool confirmed)
    {
        Show(null, (_, dialog) =>
        {
            var button = Buttons(dialog)[index];

            Press(button, Key.Enter, Keyboard.KeyDownEvent);

            Assert.Equal(confirmed, dialog.IsConfirmed);
            Assert.False(dialog.IsVisible);
        });
    }

    [Fact]
    public void Escape_Cancels()
    {
        Show(null, (_, dialog) =>
        {
            Press(Buttons(dialog)[1], Key.Escape, Keyboard.PreviewKeyDownEvent);

            Assert.False(dialog.IsConfirmed);
            Assert.False(dialog.IsVisible);
        });
    }

    [Theory]
    [InlineData(nameof(Strings.Cancel), false)]
    [InlineData(nameof(Strings.ConfirmDelete), true)]
    [InlineData(nameof(Strings.CaptionClose), false)]
    public void Click_AnswersAndCloses(string name, bool confirmed)
    {
        Show(null, (_, dialog) =>
        {
            var text = Strings.ResourceManager.GetString(name, Strings.Culture)!;
            var button = AppTheme.Descendants<Button>(dialog).Single(button => new ButtonAutomationPeer(button).GetName() == text);

            PageViews.Click(button);

            Assert.Equal(confirmed, dialog.IsConfirmed);
            Assert.False(dialog.IsVisible);
        });
    }

    private static void Show(string? detail, Action<Window, ConfirmDialog> test, bool ownerOnScreen = false) => AppTheme.Run(panel =>
    {
        var owner = Window.GetWindow(panel)!;
        if (ownerOnScreen)
        {
            owner.Left = 100;
            owner.Top = 100;
            owner.Width = 600;
            owner.Height = 400;
            OffscreenWindow.WaitUntilIdle();
        }

        var dialog = ConfirmDialog.For(owner, Question, detail);
        dialog.ShowActivated = false;
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        dialog.Show();
        OffscreenWindow.WaitUntilIdle();
        try
        {
            test(owner, dialog);
        }
        finally
        {
            if (!closed)
            {
                dialog.Close();
            }
        }
    });

    // A dialog that does not show, for what it asks.
    private static void Make(Func<Window, ConfirmDialog> make, Action<Window, ConfirmDialog> test) => AppTheme.Run(panel =>
    {
        var owner = Window.GetWindow(panel)!;
        var dialog = make(owner);
        try
        {
            test(owner, dialog);
        }
        finally
        {
            dialog.Close();
        }
    });

    // Annuleren and Verwijderen, in the order of Tab; the cross is not in it.
    private static Button[] Buttons(Window dialog) => [.. AppTheme.Descendants<Button>(dialog).Where(button => button.Focusable)];

    private static IEnumerable<TextBlock> Texts(Window dialog) => AppTheme.Descendants<TextBlock>(dialog);

    private static void Press(UIElement element, Key key, RoutedEvent routedEvent)
    {
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key) { RoutedEvent = routedEvent });
        OffscreenWindow.WaitUntilIdle();
    }
}
