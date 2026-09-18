namespace RemoteHub.Core.Models;

/// <summary>
/// Visual-experience and connection-resilience preferences for an <see cref="RdpConnection"/>. The
/// visual toggles are what mstsc's Experience tab shows; they trade fidelity for bandwidth. Everything
/// defaults to on, as mstsc does on a LAN — note the bare control leaves font smoothing and desktop
/// composition off, so connections saved before these settings existed gain both.
/// </summary>
public sealed class RdpExperienceSettings
{
    // TS_PERF_* bits of the RDP control's PerformanceFlags. The first group DISABLES a feature when
    // set, the last two ENABLE one — hence the mixed polarity in ToPerformanceFlags.
    private const int DisableWallpaper = 0x01;
    private const int DisableFullWindowDrag = 0x02;
    private const int DisableMenuAnimations = 0x04;
    private const int DisableTheming = 0x08;
    private const int EnableFontSmoothing = 0x80;
    private const int EnableDesktopComposition = 0x100;

    public bool DesktopBackground { get; set; } = true;

    /// <summary>ClearType text in the remote session.</summary>
    public bool FontSmoothing { get; set; } = true;

    public bool DesktopComposition { get; set; } = true;

    public bool ShowWindowContentsWhileDragging { get; set; } = true;

    public bool MenuAnimations { get; set; } = true;

    public bool VisualStyles { get; set; } = true;

    /// <summary>Caches bitmaps on disk across sessions so reconnecting redraws faster.</summary>
    public bool PersistentBitmapCaching { get; set; } = true;

    /// <summary>Lets the control try to re-establish a session whose network connection dropped.</summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>The value for the RDP control's <c>PerformanceFlags</c> property.</summary>
    public int ToPerformanceFlags()
    {
        var flags = 0;
        if (!DesktopBackground) flags |= DisableWallpaper;
        if (!ShowWindowContentsWhileDragging) flags |= DisableFullWindowDrag;
        if (!MenuAnimations) flags |= DisableMenuAnimations;
        if (!VisualStyles) flags |= DisableTheming;
        if (FontSmoothing) flags |= EnableFontSmoothing;
        if (DesktopComposition) flags |= EnableDesktopComposition;
        return flags;
    }
}
