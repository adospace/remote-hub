using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;

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
/// The ViewModel holds no reference to the hosted ActiveX control: it raises intent events that
/// the owning <c>RdpSessionView</c> handles, and the view pushes state back via <see cref="Status"/>.
/// </summary>
public sealed partial class SessionViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(IsConnecting))]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    [NotifyPropertyChangedFor(nameof(IsSessionActive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private SessionStatus _status = SessionStatus.Disconnected;

    [ObservableProperty]
    private string _title = string.Empty;

    private readonly ICredentialProtector _protector;

    public SessionViewModel(RdpConnection connection, ICredentialProtector protector)
    {
        Connection = connection;
        _protector = protector;
        Title = string.IsNullOrWhiteSpace(connection.Name) ? connection.Host : connection.Name;
    }

    public RdpConnection Connection { get; }

    /// <summary>The protector, exposed so a pop-out window can build its own session.</summary>
    public ICredentialProtector Protector => _protector;

    /// <summary>
    /// Decrypts the saved password for this connection, or returns null when none is stored or the
    /// vault is locked. The plaintext is used transiently to configure the control and not retained.
    /// </summary>
    public string? ResolvePassword() =>
        !string.IsNullOrEmpty(Connection.EncryptedPassword) &&
        _protector.TryDecrypt(Connection.EncryptedPassword, out var plain)
            ? plain
            : null;

    public bool IsConnected => Status == SessionStatus.Connected;
    public bool IsConnecting => Status == SessionStatus.Connecting;
    public bool IsDisconnected => Status == SessionStatus.Disconnected;

    /// <summary>True whenever the remote surface should be shown (connected or connecting).</summary>
    public bool IsSessionActive => Status != SessionStatus.Disconnected;

    public string StatusText => Status switch
    {
        SessionStatus.Connected => "Connected",
        SessionStatus.Connecting => $"Connecting to {Connection.Host}…",
        _ => "Disconnected",
    };

    /// <summary>Optional detail (e.g. disconnect reason) shown on the disconnected overlay.</summary>
    [ObservableProperty]
    private string? _statusDetail;

    // Intent events consumed by the hosting RdpSessionView (control-level operations) and, for
    // CloseRequested, by the SessionsViewModel/MainWindow container.
    public event EventHandler? ConnectRequested;
    public event EventHandler? DisconnectRequested;
    public event EventHandler? ReconnectRequested;
    public event EventHandler? FullScreenRequested;
    public event EventHandler? PopOutRequested;
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void Connect() => ConnectRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Disconnect() => DisconnectRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Reconnect() => ReconnectRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void FullScreen() => FullScreenRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void PopOut() => PopOutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Close()
    {
        // Ensure the control tears down, then ask the container to drop the tab.
        DisconnectRequested?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
