using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// The automation the user wants for a profile.
public sealed record ProfileAutomationChange(string Name, ProfileAutomation Automation);
