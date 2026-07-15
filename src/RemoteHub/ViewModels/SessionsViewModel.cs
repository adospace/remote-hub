using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Models;

namespace RemoteHub.ViewModels;

/// <summary>
/// Holds the collection of open RDP session tabs and the current selection.
/// </summary>
public sealed partial class SessionsViewModel : ObservableObject
{
    [ObservableProperty]
    private SessionViewModel? _selectedSession;

    public ObservableCollection<SessionViewModel> Sessions { get; } = new();

    /// <summary>Opens a new session for the connection and selects it.</summary>
    public SessionViewModel OpenSession(RdpConnection connection)
    {
        var session = new SessionViewModel(connection);
        Sessions.Add(session);
        SelectedSession = session;
        return session;
    }

    /// <summary>Closes and removes a session tab.</summary>
    public void CloseSession(SessionViewModel session)
    {
        Sessions.Remove(session);
        if (ReferenceEquals(SelectedSession, session))
        {
            SelectedSession = Sessions.Count > 0 ? Sessions[^1] : null;
        }
    }
}
