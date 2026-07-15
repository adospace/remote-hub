using System.Windows;
using RemoteHub.Core.Models;
using RemoteHub.ViewModels;

namespace RemoteHub.Controls;

/// <summary>
/// A standalone top-level window that hosts a fresh <see cref="RdpSessionView"/> for one
/// connection. Used by the pop-out command: rather than reparent the live ActiveX control (which
/// is unstable), we create a new session/view here and connect it independently.
/// </summary>
public partial class RdpSessionWindow : Window
{
    private readonly SessionViewModel _session;

    public RdpSessionWindow(RdpConnection connection)
    {
        _session = new SessionViewModel(connection);
        DataContext = _session;
        InitializeComponent();

        // Auto-connect once the window (and its hosted control) is realized.
        Loaded += (_, _) => _session.ConnectCommand.Execute(null);
        Closed += (_, _) => _session.DisconnectCommand.Execute(null);
    }

    /// <summary>The session driving this pop-out window.</summary>
    public SessionViewModel Session => _session;
}
