using System.Windows;
using RemoteHub.Diagnostics;
using RemoteHub.ViewModels;
using WinForms = System.Windows.Forms;
using UserControl = System.Windows.Controls.UserControl;

namespace RemoteHub.Controls;

/// <summary>
/// Hosts a single RDP session. A <c>WindowsFormsHost</c> wraps a WinForms <see cref="WinForms.Panel"/>;
/// the <see cref="RdpClientHost"/> ActiveX control is created lazily and added to that panel only on
/// connect (a user action) — never while the WPF window is laying out, which would reenter the
/// dispatcher and crash. The bound <see cref="SessionViewModel"/> raises intent events that this
/// view turns into control operations; the view pushes connection state back onto the ViewModel.
/// </summary>
public partial class RdpSessionView : UserControl
{
    private readonly WinForms.Panel _panel = new() { BackColor = System.Drawing.Color.Black };
    private RdpClientHost? _client;
    private SessionViewModel? _vm;
    private bool _reconnectPending;

    public RdpSessionView()
    {
        InitializeComponent();
        RdpHostContainer.Child = _panel;

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    /// <summary>Creates the ActiveX host on first use and adds it to the WinForms panel.</summary>
    private RdpClientHost EnsureClient()
    {
        if (_client is null)
        {
            _client = new RdpClientHost { Dock = WinForms.DockStyle.Fill };
            _client.Connected += OnClientConnected;
            _client.Disconnected += OnClientDisconnected;
            _panel.Controls.Add(_client); // realizes + activates the ActiveX here (outside WPF layout)
        }

        return _client;
    }

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
            Log.Info($"Session view bound to '{_vm.Connection.Host}'.");
            _vm.ConnectRequested += OnConnectRequested;
            _vm.DisconnectRequested += OnDisconnectRequested;
            _vm.ReconnectRequested += OnReconnectRequested;
            _vm.FullScreenRequested += OnFullScreenRequested;
            _vm.PopOutRequested += OnPopOutRequested;
        }
    }

    private void OnConnectRequested(object? sender, EventArgs e)
    {
        if (_vm is null) return;

        RdpClientHost client;
        try
        {
            client = EnsureClient();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to create the RDP control.", ex);
            _vm.Status = SessionStatus.Disconnected;
            _vm.StatusDetail = "Could not initialize the remote desktop control: " + ex.Message;
            return;
        }

        if (client.IsConnected) return;

        _vm.StatusDetail = null;
        _vm.Status = SessionStatus.Connecting;
        try
        {
            client.Setup(_vm.Connection, _vm.ResolvePassword());
            client.Connect();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to start the RDP connection.", ex);
            _vm.Status = SessionStatus.Disconnected;
            _vm.StatusDetail = ex.Message;
        }
    }

    private void OnDisconnectRequested(object? sender, EventArgs e) => _client?.Disconnect();

    private void OnReconnectRequested(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        if (_client is { IsConnected: true })
        {
            // Defer the connect until the control reports it has fully torn down.
            _reconnectPending = true;
            _client.Disconnect();
        }
        else
        {
            OnConnectRequested(sender, e);
        }
    }

    private void OnFullScreenRequested(object? sender, EventArgs e) => _client?.SetFullScreen(true);

    private void OnPopOutRequested(object? sender, EventArgs e)
    {
        if (_vm is null) return;

        // A pop-out gets a fresh window/session for the same connection (live ActiveX reparenting is
        // avoided for stability), then the in-tab session is dropped.
        var window = new RdpSessionWindow(_vm.Connection, _vm.Protector)
        {
            Owner = Window.GetWindow(this),
        };

        // The pop-out builds its own SessionViewModel, which nothing has wired to the editor, so its
        // toolbar Edit button would be inert. Forward it through the in-tab session (still alive —
        // pop-out only disconnects it) to reach MainViewModel. Both sessions share one RdpConnection,
        // so this edits the right thing. If the tab is closed while the pop-out stays open, the
        // forward goes nowhere and its Edit button stops working — the main window still edits it.
        window.Session.EditRequested += (_, _) => _vm.EditCommand.Execute(null);

        _client?.Disconnect();
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
        if (_client is { IsDisposed: false })
        {
            _client.Disconnect();
        }
    }
}
