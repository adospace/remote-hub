namespace RemoteHub.Services;

/// <summary>
/// Checks for, downloads, and applies application updates. Implementations must no-op gracefully
/// when updates are unavailable (debug builds, or a copy that was not installed by the installer).
/// </summary>
public interface IUpdateService
{
    /// <summary>False when updates are disabled (no feed configured, or no installer context).</summary>
    bool IsSupported { get; }

    string CurrentVersion { get; }

    Task<UpdateInfo?> CheckForUpdatesAsync();

    Task<bool> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null);

    /// <summary>
    /// Applies a downloaded update and restarts. Does not return on success; returns false when the
    /// update could not be applied, so the caller can surface the failure.
    /// </summary>
    bool ApplyUpdatesAndRestart(UpdateInfo updateInfo);

    event EventHandler<UpdateStatus>? StatusChanged;
}

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;

    public string ReleaseNotes { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>The provider's own update object (here: <c>Velopack.UpdateInfo</c>), passed back on download/apply.</summary>
    public object NativeUpdateInfo { get; set; } = null!;
}

public enum UpdateStatus
{
    Checking,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    NoUpdateAvailable,
    Error,
}

public class UpdateOptions
{
    /// <summary>GitHub repository serving the release feed. Empty disables updates entirely.</summary>
    public string RepoUrl { get; set; } = string.Empty;
}
