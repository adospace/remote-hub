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

    /// <summary>
    /// Raised when a session asks for its connection to be edited. This view-model has no dialogs or
    /// store, so it only relays: MainViewModel listens and does the work.
    /// </summary>
    public event EventHandler<SessionViewModel>? EditSessionRequested;

    /// <summary>True when at least one session tab is open (drives the content vs. empty-state view).</summary>
    public bool HasSessions => Sessions.Count > 0;

    /// <summary>True when no session tabs are open.</summary>
    public bool NoSessions => Sessions.Count == 0;

    /// <summary>
    /// Opens a session for the connection and selects it. If a tab is already open for the SAME
    /// connection — matched by reference (the tree reuses the same <see cref="RdpConnection"/>
    /// instances) and, as a fallback, by <see cref="ConnectionNode.Id"/> — that existing tab is
    /// re-selected and returned instead of opening a duplicate.
    /// </summary>
    public SessionViewModel OpenSession(RdpConnection connection)
    {
        var existing = Sessions.FirstOrDefault(
            s => ReferenceEquals(s.Connection, connection) || s.Connection.Id == connection.Id);
        if (existing is not null)
        {
            SelectedSession = existing;
            return existing;
        }

        var session = new SessionViewModel(connection, _protector);
        session.EditRequested += OnSessionEditRequested;
        Sessions.Add(session);
        SelectedSession = session;
        return session;
    }

    private void OnSessionEditRequested(object? sender, EventArgs e)
    {
        if (sender is SessionViewModel session)
        {
            EditSessionRequested?.Invoke(this, session);
        }
    }

    /// <summary>Closes and removes a session tab.</summary>
    public void CloseSession(SessionViewModel session)
    {
        session.EditRequested -= OnSessionEditRequested;
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
