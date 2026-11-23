using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// A tile of the effect choice on Kleur (DESIGN.md § 9.3 and 9.8): a radio button, so a screen reader
// says "keuzerondje" and whether it is chosen. A tile that may not choose (CanChoose false: "Eigen
// effect" without own effects, besluit 10) does not check itself on a click, Space or Select() of UI
// Automation, but still runs its command, so the page can show "Jouw effect"; the tile that was
// chosen stays chosen. Which tile is chosen comes from the view model (IsChecked bound to it).
public sealed class EffectTile : RadioButton
{
    public static readonly DependencyProperty CanChooseProperty = DependencyProperty.Register(
        nameof(CanChoose), typeof(bool), typeof(EffectTile), new PropertyMetadata(true));

    public bool CanChoose
    {
        get => (bool)GetValue(CanChooseProperty);
        set => SetValue(CanChooseProperty, value);
    }

    // As a click: the tile checks itself if it may, and runs its command.
    internal void Choose() => OnClick();

    protected override void OnToggle()
    {
        if (CanChoose)
        {
            base.OnToggle();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EffectTileAutomationPeer(this);

    // RadioButtonAutomationPeer checks the button itself on Select(), past OnToggle and its command;
    // this one lets Select() go the way of a click.
    private sealed class EffectTileAutomationPeer(EffectTile owner) : RadioButtonAutomationPeer(owner), ISelectionItemProvider
    {
        bool ISelectionItemProvider.IsSelected => owner.IsChecked == true;

        IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => null;

        protected override string GetClassNameCore() => nameof(EffectTile);

        void ISelectionItemProvider.Select()
        {
            if (!IsEnabled())
            {
                throw new ElementNotEnabledException();
            }

            owner.Choose();
        }

        void ISelectionItemProvider.AddToSelection() => throw new InvalidOperationException();

        void ISelectionItemProvider.RemoveFromSelection() => throw new InvalidOperationException();
    }
}
