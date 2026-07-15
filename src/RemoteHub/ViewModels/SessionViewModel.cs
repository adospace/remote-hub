using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteHub.Core.Models;

namespace RemoteHub.ViewModels;

/// <summary>
/// Connection status for an open RDP session.
/// </summary>
public enum SessionStatus
{
    Disconnected,
    Connecting,
    Connected,
}

/// <summary>
/// One open RDP session/tab, wrapping a single <see cref="RdpConnection"/>.
/// </summary>
public sealed partial class SessionViewModel : ObservableObject
{
    [ObservableProperty]
    private SessionStatus _status = SessionStatus.Disconnected;

    public SessionViewModel(RdpConnection connection)
    {
        Connection = connection;
        Title = connection.Name;
    }

    public RdpConnection Connection { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    // TODO(Implement): wire these to the RdpSessionView's hosted control.
    [RelayCommand]
    private void Connect() => throw new NotImplementedException();

    [RelayCommand]
    private void Disconnect() => throw new NotImplementedException();

    [RelayCommand]
    private void PopOut() => throw new NotImplementedException();

    [RelayCommand]
    private void Close() => throw new NotImplementedException();
}
