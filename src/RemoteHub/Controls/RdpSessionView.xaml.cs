using System.Windows;
using RemoteHub.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace RemoteHub.Controls;

/// <summary>
/// Hosts a single RDP session: a <c>WindowsFormsHost</c> wrapping an <see cref="RdpClientHost"/>
/// plus a session toolbar. The bound <see cref="SessionViewModel"/> raises intent events
/// (connect/disconnect/reconnect/pop-out/full-screen) that this view translates into control
/// operations, and the view pushes connection state back onto the ViewModel's Status.
/// </summary>
public partial class RdpSessionView : UserControl
{
    private readonly RdpClientHost _client = new();
    private SessionViewModel? _vm;
    private bool _reconnectPending;

    public RdpSessionView()
    {
        InitializeComponent();
        RdpHostContainer.Child = _client;

        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    /// <summary>The hosted RDP ActiveX wrapper.</summary>
    public RdpClientHost Client => _client;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.ConnectRequested -= OnConnectRequested;
            _vm.DisconnectRequested -= OnDisconnectRequested;
            _vm.ReconnectRequested -= OnReconnectRequested;
            _vm.FullScreenRequested -= OnFullScreenRequested;
            _vm.PopOutRequested -= OnPopOutRequested;
        }

        _vm = e.NewValue as SessionViewModel;

        if (_vm is not null)
        {
            _vm.ConnectRequested += OnConnectRequested;
            _vm.DisconnectRequested += OnDisconnectRequested;
            _vm.ReconnectRequested += OnReconnectRequested;
            _vm.FullScreenRequested += OnFullScreenRequested;
            _vm.PopOutRequested += OnPopOutRequested;
        }
    }

    private void OnConnectRequested(object? sender, EventArgs e)
    {
        if (_vm is null || _client.IsConnected) return;
        _vm.StatusDetail = null;
        _vm.Status = SessionStatus.Connecting;
        try
        {
            _client.Setup(_vm.Connection);
            _client.Connect();
        }
        catch (Exception ex)
        {
            _vm.Status = SessionStatus.Disconnected;
            _vm.StatusDetail = ex.Message;
        }
    }

    private void OnDisconnectRequested(object? sender, EventArgs e) => _client.Disconnect();

    private void OnReconnectRequested(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        if (_client.IsConnected)
        {
            // Defer the connect until the control reports it has fully torn down
            // (handled in OnClientDisconnected) to avoid connecting a half-disposed session.
            _reconnectPending = true;
            _client.Disconnect();
        }
        else
        {
            OnConnectRequested(sender, e);
        }
    }

    private void OnFullScreenRequested(object? sender, EventArgs e) => _client.SetFullScreen(true);

    private void OnPopOutRequested(object? sender, EventArgs e)
    {
        if (_vm is null) return;

        // Live reparenting of the ActiveX is avoided for stability: open a standalone window with a
        // fresh view/session for the same connection, then drop the in-tab session.
        var window = new RdpSessionWindow(_vm.Connection)
        {
            Owner = Window.GetWindow(this),
        };
        _client.Disconnect();
        _vm.Status = SessionStatus.Disconnected;
        window.Show();
    }

    private void OnClientConnected(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            if (_vm is not null) _vm.Status = SessionStatus.Connected;
        });

    private void OnClientDisconnected(object? sender, string reason) =>
        Dispatcher.Invoke(() =>
        {
            if (_vm is null) return;
            _vm.Status = SessionStatus.Disconnected;
            _vm.StatusDetail = reason;

            if (_reconnectPending)
            {
                _reconnectPending = false;
                OnConnectRequested(this, EventArgs.Empty);
            }
        });

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Only tear down when the control is truly leaving the tree (not on tab switches, which do
        // not unload WindowsFormsHost children). Guard against double-dispose.
        if (!_client.IsDisposed)
        {
            _client.Disconnect();
        }
    }
}
