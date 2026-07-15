using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;

namespace RemoteHub.ViewModels;

/// <summary>
/// Holds the collection of open RDP session tabs and the current selection.
/// </summary>
public sealed partial class SessionsViewModel : ObservableObject
{
    private readonly ICredentialProtector _protector;

    [ObservableProperty]
    private SessionViewModel? _selectedSession;

    public SessionsViewModel(ICredentialProtector protector)
    {
        _protector = protector;
        Sessions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSessions));
            OnPropertyChanged(nameof(NoSessions));
        };
    }

    public ObservableCollection<SessionViewModel> Sessions { get; } = new();

    /// <summary>True when at least one session tab is open (drives the content vs. empty-state view).</summary>
    public bool HasSessions => Sessions.Count > 0;

    /// <summary>True when no session tabs are open.</summary>
    public bool NoSessions => Sessions.Count == 0;

    /// <summary>Opens a new session for the connection and selects it.</summary>
    public SessionViewModel OpenSession(RdpConnection connection)
    {
        var session = new SessionViewModel(connection, _protector);
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

    /// <summary>Command hook for the tab-header close button.</summary>
    [RelayCommand]
    private void CloseTab(SessionViewModel? session)
    {
        if (session is not null)
        {
            CloseSession(session);
        }
    }
}
