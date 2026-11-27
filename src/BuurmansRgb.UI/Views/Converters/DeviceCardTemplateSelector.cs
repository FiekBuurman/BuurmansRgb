using System.Windows;
using System.Windows.Controls;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// The card of a device on Kleur: one that opens and closes (DeviceCard) with "Hele huis" and two or
// more devices, otherwise the card of before, always open, without an arrow and without
// ExpandCollapse, so Verteller does not say "uitgevouwen" and its title is no Tab stop that does
// nothing (DESIGN.md § 9.8).
public sealed class DeviceCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Foldable { get; set; }

    public DataTemplate? Fixed { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is DevicePreviewViewModel { IsFoldable: true } ? Foldable : Fixed;
}
