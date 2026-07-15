using System.Windows.Forms;

namespace RemoteHub.Controls;

// Hosts the Microsoft RDP ActiveX control (mstscax.dll) without COMReference or a generated
// interop assembly. We subclass AxHost with the control CLSID and drive it via late-bound
// `dynamic` dispatch (the control is an IDispatch ActiveX object). VERIFIED to build under
// net10.0-windows with UseWindowsForms=true.
public sealed class RdpClientHost : AxHost
{
    // MsRdpClient "NotSafeForScripting" coclass CLSID (OS resolves to newest installed).
    private const string RdpClsid = "791fa017-2de3-492e-acc5-53c67a2b94d0";

    private dynamic? _ocx;

    public event EventHandler? Connected;
    public event EventHandler<string>? Disconnected;

    public RdpClientHost() : base(RdpClsid) { }

    protected override void AttachInterfaces()
    {
        base.AttachInterfaces();
        _ocx = GetOcx();
        // Wire OnConnected / OnDisconnected via dynamic event hookup if desired, or poll .Connected.
    }

    public void Setup(string server, int port, string? userName, string? domain, int width, int height)
    {
        if (_ocx is null) return;
        _ocx.Server = server;
        if (!string.IsNullOrEmpty(userName)) _ocx.UserName = userName;
        if (!string.IsNullOrEmpty(domain)) _ocx.Domain = domain;
        _ocx.DesktopWidth = width;
        _ocx.DesktopHeight = height;
        var adv = _ocx.AdvancedSettings9;   // AdvancedSettings2..9 all valid; 9 is broadly available
        adv.RDPPort = port;
        adv.RedirectClipboard = true;
        adv.EnableCredSspSupport = true;
        adv.AuthenticationLevel = 2;
    }

    public void Connect() => _ocx?.Connect();
    public void Disconnect() { try { _ocx?.Disconnect(); } catch { /* not connected */ } }
    public bool IsConnected => _ocx is not null && (int)_ocx.Connected == 1;

    // Surfaced so the compiler treats the events as used until the Implement phase wires them.
    private void RaiseConnected() => Connected?.Invoke(this, EventArgs.Empty);
    private void RaiseDisconnected(string reason) => Disconnected?.Invoke(this, reason);
}
