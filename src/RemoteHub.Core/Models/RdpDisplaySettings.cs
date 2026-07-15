namespace RemoteHub.Core.Models;

/// <summary>
/// How the remote desktop surface is sized within the session view.
/// </summary>
public enum ScreenSizeMode
{
    FitToWindow,
    FixedSize,
    FullScreen,
}

/// <summary>
/// Remote session color depth, in bits per pixel.
/// </summary>
public enum ColorDepth
{
    Bpp16 = 16,
    Bpp24 = 24,
    Bpp32 = 32,
}

/// <summary>
/// Where remote audio is played.
/// </summary>
public enum AudioRedirectionMode
{
    Local,
    Remote,
    None,
}

/// <summary>
/// Display and redirection preferences for an <see cref="RdpConnection"/>.
/// </summary>
public sealed class RdpDisplaySettings
{
    public ScreenSizeMode ScreenMode { get; set; } = ScreenSizeMode.FitToWindow;

    public int DesktopWidth { get; set; } = 1920;

    public int DesktopHeight { get; set; } = 1080;

    public ColorDepth ColorDepth { get; set; } = ColorDepth.Bpp32;

    public bool FullScreen { get; set; } = false;

    public bool RedirectClipboard { get; set; } = true;

    public AudioRedirectionMode Audio { get; set; } = AudioRedirectionMode.Local;
}
