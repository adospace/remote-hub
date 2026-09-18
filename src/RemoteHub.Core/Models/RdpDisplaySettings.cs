namespace RemoteHub.Core.Models;

/// <summary>
/// How the remote desktop surface is sized within the session view.
/// </summary>
public enum ScreenSizeMode
{
    /// <summary>The remote desktop keeps its resolution and is scaled to fit the window.</summary>
    FitToWindow,

    /// <summary>The remote desktop is shown at its own resolution, unscaled.</summary>
    FixedSize,

    /// <summary>The session goes full screen once connected, at the resolution of its monitor.</summary>
    FullScreen,
}

/// <summary>
/// Remote session color depth, in bits per pixel.
/// </summary>
public enum ColorDepth
{
    Bpp15 = 15,
    Bpp16 = 16,
    Bpp24 = 24,
    Bpp32 = 32,
}

/// <summary>
/// Where remote audio is played. Values match the RDP control's <c>AudioRedirectionMode</c>.
/// </summary>
public enum AudioRedirectionMode
{
    Local = 0,
    Remote = 1,
    None = 2,
}

/// <summary>
/// Where Windows key combinations (Alt+Tab, Win, …) go. Values match the RDP control's
/// <c>KeyboardHookMode</c>.
/// </summary>
public enum KeyboardHookMode
{
    Local = 0,
    Remote = 1,
    FullScreenOnly = 2,
}

/// <summary>
/// Display and redirection preferences for an <see cref="RdpConnection"/>. Defaults match what the
/// RDP control does when left alone, so a connection saved before a setting existed keeps behaving
/// the same.
/// </summary>
public sealed class RdpDisplaySettings
{
    public ScreenSizeMode ScreenMode { get; set; } = ScreenSizeMode.FitToWindow;

    public int DesktopWidth { get; set; } = 1920;

    public int DesktopHeight { get; set; } = 1080;

    public ColorDepth ColorDepth { get; set; } = ColorDepth.Bpp32;

    /// <summary>Shows the drop-down connection bar while the session is full screen.</summary>
    public bool DisplayConnectionBar { get; set; } = true;

    /// <summary>Keeps the full-screen connection bar pinned open rather than auto-hiding.</summary>
    public bool PinConnectionBar { get; set; } = true;

    public bool RedirectClipboard { get; set; } = true;

    public AudioRedirectionMode Audio { get; set; } = AudioRedirectionMode.Local;

    /// <summary>Makes this computer's microphone available to the remote session.</summary>
    public bool RecordAudio { get; set; }

    public KeyboardHookMode KeyboardHook { get; set; } = KeyboardHookMode.FullScreenOnly;

    public bool RedirectPrinters { get; set; }

    /// <summary>Makes every local drive available to the remote session.</summary>
    public bool RedirectDrives { get; set; }

    public bool RedirectSmartCards { get; set; }

    /// <summary>Makes local serial and parallel ports available to the remote session.</summary>
    public bool RedirectPorts { get; set; }
}
