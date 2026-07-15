using System.Windows.Forms;
using RemoteHub.Core.Models;

namespace RemoteHub.Controls;

// Hosts the Microsoft RDP ActiveX control (mstscax.dll) without COMReference or a generated
// interop assembly. We subclass AxHost with the control CLSID and drive it via late-bound
// `dynamic` dispatch (the control is an IDispatch ActiveX object). VERIFIED to build under
// net10.0-windows with UseWindowsForms=true.
//
// Connection-state notifications are surfaced by polling the control's `Connected` property on a
// WinForms timer. Subscribing to the ActiveX event sink would require IConnectionPoint plumbing
// and a generated sink interface; polling is simpler and reliable, and the spec explicitly
// permits it (§7).
public sealed class RdpClientHost : AxHost
{
    // MsRdpClient "NotSafeForScripting" coclass CLSID (OS resolves to newest installed).
    private const string RdpClsid = "791fa017-2de3-492e-acc5-53c67a2b94d0";

    private dynamic? _ocx;
    // Fully qualified: System.Threading.Timer is also in scope via implicit usings.
    private readonly System.Windows.Forms.Timer _stateTimer = new() { Interval = 400 };
    private bool _wasConnected;
    private bool _connectAttempted;

    public event EventHandler? Connected;
    public event EventHandler<string>? Disconnected;

    public RdpClientHost() : base(RdpClsid)
    {
        _stateTimer.Tick += OnStatePollTick;
    }

    protected override void AttachInterfaces()
    {
        base.AttachInterfaces();
        _ocx = GetOcx();
    }

    /// <summary>
    /// Applies connection + display settings from the model onto the ActiveX control. When a
    /// <paramref name="password"/> is supplied (decrypted from the vault at connect time) it is
    /// passed to the control for auto-logon; otherwise CredSSP prompts for credentials. The
    /// plaintext password is only ever held transiently here and never persisted.
    /// </summary>
    public void Setup(RdpConnection connection, string? password = null)
    {
        if (_ocx is null) return;

        var display = connection.Display;
        var (width, height) = ResolveDesktopSize(display);

        _ocx.Server = connection.Host;
        if (!string.IsNullOrEmpty(connection.Username)) _ocx.UserName = connection.Username;
        if (!string.IsNullOrEmpty(connection.Domain)) _ocx.Domain = connection.Domain;
        _ocx.DesktopWidth = width;
        _ocx.DesktopHeight = height;
        _ocx.ColorDepth = (int)display.ColorDepth;

        var adv = _ocx.AdvancedSettings9;   // AdvancedSettings2..9 all valid; 9 is broadly available
        adv.RDPPort = connection.Port <= 0 ? 3389 : connection.Port;
        adv.RedirectClipboard = display.RedirectClipboard;
        adv.EnableCredSspSupport = true;
        adv.AuthenticationLevel = 2;
        adv.AudioRedirectionMode = (int)display.Audio;   // Local=0, Remote=1, None=2 (matches enum order)
        // FitToWindow scales the remote surface to the host size instead of showing scrollbars.
        adv.SmartSizing = display.ScreenMode == ScreenSizeMode.FitToWindow;

        // Auto-logon with the saved password when available. ClearTextPassword must be set after
        // UserName; the control keeps it in memory only for the duration of the connection.
        if (!string.IsNullOrEmpty(password))
        {
            try { adv.ClearTextPassword = password; } catch { /* control may reject in rare policies */ }
        }
    }

    /// <summary>Backwards-compatible primitive Setup overload (kept for the verified §7 signature).</summary>
    public void Setup(string server, int port, string? userName, string? domain, int width, int height)
    {
        if (_ocx is null) return;
        _ocx.Server = server;
        if (!string.IsNullOrEmpty(userName)) _ocx.UserName = userName;
        if (!string.IsNullOrEmpty(domain)) _ocx.Domain = domain;
        _ocx.DesktopWidth = width;
        _ocx.DesktopHeight = height;
        var adv = _ocx.AdvancedSettings9;
        adv.RDPPort = port;
        adv.RedirectClipboard = true;
        adv.EnableCredSspSupport = true;
        adv.AuthenticationLevel = 2;
    }

    public void Connect()
    {
        if (_ocx is null) return;
        _connectAttempted = true;
        _ocx.Connect();
        _stateTimer.Start();
    }

    public void Disconnect()
    {
        try { _ocx?.Disconnect(); } catch { /* not connected */ }
    }

    public bool IsConnected => _ocx is not null && SafeConnectedState() == 1;

    /// <summary>Toggles the control's own full-screen mode (a separate top-level RDP window).</summary>
    public void SetFullScreen(bool value)
    {
        if (_ocx is null || SafeConnectedState() != 1) return;
        try { _ocx.FullScreen = value; } catch { /* control rejects when not connected */ }
    }

    private void OnStatePollTick(object? sender, EventArgs e)
    {
        // 0 = disconnected, 1 = connected, 2 = connecting.
        var state = SafeConnectedState();

        if (state == 1 && !_wasConnected)
        {
            _wasConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (state == 0 && _connectAttempted)
        {
            // Fires for both a dropped live session (1 -> 0) and a failed connect (2 -> 0),
            // so the UI never gets stuck in the "Connecting" state.
            _connectAttempted = false;
            _wasConnected = false;
            _stateTimer.Stop();
            Disconnected?.Invoke(this, DescribeDisconnect());
        }
    }

    // Connected returns 0 (disconnected), 1 (connected) or 2 (connecting). Guard against the
    // control not yet being realized or throwing mid-teardown.
    private int SafeConnectedState()
    {
        try { return _ocx is null ? 0 : (int)_ocx.Connected; }
        catch { return 0; }
    }

    private string DescribeDisconnect()
    {
        try
        {
            int reason = (int)_ocx!.ExtendedDisconnectReason;
            return reason == 0
                ? "The remote session was disconnected."
                : $"The remote session was disconnected (reason {reason}).";
        }
        catch
        {
            return "The remote session was disconnected.";
        }
    }

    private static (int width, int height) ResolveDesktopSize(RdpDisplaySettings display) =>
        display.ScreenMode == ScreenSizeMode.FixedSize
            ? (display.DesktopWidth, display.DesktopHeight)
            : (Math.Max(display.DesktopWidth, 800), Math.Max(display.DesktopHeight, 600));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stateTimer.Stop();
            _stateTimer.Tick -= OnStatePollTick;
            _stateTimer.Dispose();
            Disconnect();
        }
        base.Dispose(disposing);
    }
}
