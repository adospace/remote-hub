using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RemoteHub.Core.Models;
using RemoteHub.Diagnostics;

namespace RemoteHub.Controls;

// Hosts the Microsoft RDP ActiveX control (mstscax.dll) without COMReference or a generated
// interop assembly. We subclass AxHost with the control's CLSID and drive it via late-bound
// `dynamic` dispatch. The exact coclass CLSID varies by Windows version, so we PROBE at runtime
// for the newest one that actually instantiates rather than hard-coding a single GUID.
//
// IMPORTANT: this control must NOT be assigned directly as a WindowsFormsHost.Child and realized
// during a WPF layout pass — its OLE in-place activation pumps messages and reenters the WPF
// dispatcher ("Dispatcher processing has been suspended…"). The hosting view adds it to a WinForms
// Panel on demand (see RdpSessionView), which keeps activation outside WPF layout.
public sealed class RdpClientHost : AxHost
{
    // NotSafeForScripting coclasses (support ClearTextPassword), newest first. AdvancedSettings9 is
    // available on v9+, so those are preferred; older ones are fallbacks for down-level Windows.
    private static readonly string[] CandidateClsids =
    {
        "A0C63C30-F08D-4AB4-907C-34905D770C7D", // MsRdpClient11
        "8B918B82-7985-4C24-89DF-C33AD2BBFBCD", // MsRdpClient10
        "A3BC03A0-041D-42E3-AD22-882B7865C9C5", // MsRdpClient9
        "54D38BF7-B1EF-4479-9674-1BD6EA465258", // MsRdpClient8
        "D2EA46A7-C2BF-426B-AF24-E19C44456399", // MsRdpClient7
        "4EB2F086-C818-447E-B32C-C51CE2B30D31", // MsRdpClient6
    };

    private static string? _resolvedClsid;

    private dynamic? _ocx;
    private readonly System.Windows.Forms.Timer _stateTimer = new() { Interval = 400 };
    private bool _wasConnected;
    private bool _connectAttempted;

    public event EventHandler? Connected;
    public event EventHandler<string>? Disconnected;

    public RdpClientHost() : base(ResolveClsid())
    {
        _stateTimer.Tick += OnStatePollTick;
    }

    /// <summary>Probes the candidate coclasses and caches the first that can be instantiated.</summary>
    private static string ResolveClsid()
    {
        if (_resolvedClsid is not null)
        {
            return _resolvedClsid;
        }

        foreach (var clsid in CandidateClsids)
        {
            try
            {
                var type = Type.GetTypeFromCLSID(new Guid(clsid), throwOnError: false);
                if (type is null)
                {
                    continue;
                }

                var probe = Activator.CreateInstance(type);
                if (probe is not null)
                {
                    Marshal.FinalReleaseComObject(probe);
                    _resolvedClsid = clsid;
                    Log.Info($"RDP control resolved to CLSID {{{clsid}}}.");
                    return clsid;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"RDP CLSID {{{clsid}}} not creatable: {ex.Message}");
            }
        }

        _resolvedClsid = CandidateClsids[^1];
        Log.Error($"No RDP ActiveX CLSID could be instantiated; falling back to {{{_resolvedClsid}}}.");
        return _resolvedClsid;
    }

    protected override void AttachInterfaces()
    {
        base.AttachInterfaces();
        _ocx = GetOcx();
        Log.Info("RDP OCX attached (ocx " + (_ocx is null ? "null" : "ready") + ").");
    }

    /// <summary>
    /// Applies connection + display settings. When a <paramref name="password"/> is supplied it is
    /// passed to the control for auto-logon (never persisted). Every setting is applied defensively
    /// so a property missing on an older control version cannot break the whole configuration.
    /// </summary>
    public void Setup(RdpConnection connection, string? password = null)
    {
        if (_ocx is null)
        {
            Log.Warn("RDP Setup called but OCX is not ready.");
            return;
        }

        var display = connection.Display;
        var (width, height) = ResolveDesktopSize(display);

        TrySet(() => _ocx!.Server = connection.Host);
        if (!string.IsNullOrEmpty(connection.Username)) TrySet(() => _ocx!.UserName = connection.Username);
        if (!string.IsNullOrEmpty(connection.Domain)) TrySet(() => _ocx!.Domain = connection.Domain);
        TrySet(() => _ocx!.DesktopWidth = width);
        TrySet(() => _ocx!.DesktopHeight = height);
        TrySet(() => _ocx!.ColorDepth = (int)display.ColorDepth);

        dynamic? adv = GetBestAdvancedSettings();
        if (adv is not null)
        {
            TrySet(() => adv.RDPPort = connection.Port <= 0 ? 3389 : connection.Port);
            TrySet(() => adv.RedirectClipboard = display.RedirectClipboard);
            TrySet(() => adv.EnableCredSspSupport = true);
            TrySet(() => adv.AuthenticationLevel = 2);
            TrySet(() => adv.AudioRedirectionMode = (int)display.Audio); // Local=0, Remote=1, None=2
            TrySet(() => adv.SmartSizing = display.ScreenMode == ScreenSizeMode.FitToWindow);
            if (!string.IsNullOrEmpty(password))
            {
                TrySet(() => adv.ClearTextPassword = password);
            }
        }
    }

    public void Connect()
    {
        if (_ocx is null) return;
        try
        {
            _connectAttempted = true;
            _ocx.Connect();
            _stateTimer.Start();
            Log.Info("RDP Connect() invoked.");
        }
        catch (Exception ex)
        {
            Log.Error("RDP Connect() failed.", ex);
            throw;
        }
    }

    public void Disconnect()
    {
        try { _ocx?.Disconnect(); } catch { /* not connected */ }
    }

    public bool IsConnected => _ocx is not null && SafeConnectedState() == 1;

    /// <summary>Toggles the control's own full-screen mode.</summary>
    public void SetFullScreen(bool value)
    {
        if (_ocx is null || SafeConnectedState() != 1) return;
        try { _ocx.FullScreen = value; } catch { /* rejected when not connected */ }
    }

    /// <summary>Returns the highest available AdvancedSettingsN object, or null.</summary>
    private object? GetBestAdvancedSettings()
    {
        if (_ocx is null) return null;
        object ocx = _ocx;
        foreach (var name in new[]
                 {
                     "AdvancedSettings9", "AdvancedSettings8", "AdvancedSettings7", "AdvancedSettings6",
                     "AdvancedSettings5", "AdvancedSettings4", "AdvancedSettings3", "AdvancedSettings2",
                 })
        {
            try
            {
                var settings = ocx.GetType().InvokeMember(name, BindingFlags.GetProperty, null, ocx, null);
                if (settings is not null)
                {
                    return settings;
                }
            }
            catch
            {
                // property not present on this control version — try the next one down
            }
        }

        return null;
    }

    private static void TrySet(Action set)
    {
        try { set(); }
        catch (Exception ex) { Log.Warn("RDP setting rejected: " + ex.Message); }
    }

    private void OnStatePollTick(object? sender, EventArgs e)
    {
        var state = SafeConnectedState(); // 0 disconnected, 1 connected, 2 connecting

        if (state == 1 && !_wasConnected)
        {
            _wasConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (state == 0 && _connectAttempted)
        {
            _connectAttempted = false;
            _wasConnected = false;
            _stateTimer.Stop();
            Disconnected?.Invoke(this, DescribeDisconnect());
        }
    }

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
