using CommunityToolkit.Mvvm.ComponentModel;

namespace RemoteHub.ViewModels;

/// <summary>
/// Common base for view-models. Currently just <see cref="ObservableObject"/>; kept as a seam
/// for shared behavior (busy state, error surfacing) added during implementation.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
