using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteHub.Diagnostics;
using RemoteHub.Services;

namespace RemoteHub.ViewModels;

/// <summary>
/// Drives the update banner: checks shortly after startup, auto-downloads a newer version, then
/// offers "Restart to update". Update failures are never fatal — the banner just stays hidden.
/// </summary>
public sealed partial class UpdateViewModel : ObservableObject
{
    private readonly IUpdateService _updateService;
    private UpdateInfo? _pendingUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateVersion = string.Empty;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private bool _isUpdateDownloaded;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Gates the Retry button. Separate from <see cref="HasError"/>: retrying only makes sense for a
    /// failed download, never for a failed install (which would re-download an already-downloaded
    /// update instead of retrying the install).
    /// </summary>
    [ObservableProperty]
    private bool _canRetryDownload;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public UpdateViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public string AppVersion => _updateService.CurrentVersion;

    /// <summary>
    /// Startup check, delayed so it never competes with the window's first paint. Fire-and-forget:
    /// this must not throw into the caller.
    /// </summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            await Task.Delay(5000);
            Log.Info($"Startup update check beginning. Current version: {_updateService.CurrentVersion}.");
            await TryOfferUpdateAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Startup update check failed.", ex);
        }
    }

    /// <summary>Manual check (e.g. from settings).</summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        Log.Info("Manual update check triggered.");
        StatusText = "Checking for updates...";
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            if (!await TryOfferUpdateAsync())
            {
                StatusText = "You're up to date.";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Could not check for updates.";
            Log.Error("Manual update check failed.", ex);
        }
    }

    /// <summary>Checks the feed and, if a newer version exists, shows the banner and downloads it.</summary>
    /// <returns>True when an update was offered.</returns>
    private async Task<bool> TryOfferUpdateAsync()
    {
        var update = await _updateService.CheckForUpdatesAsync();
        if (update is null)
        {
            Log.Info("No updates available.");
            return false;
        }

        if (!IsVersionNewer(update.Version, _updateService.CurrentVersion))
        {
            Log.Info($"Feed returned v{update.Version}, but v{_updateService.CurrentVersion} is same or newer. Ignoring.");
            return false;
        }

        // A fresh offer must not inherit flags from a previous cycle (e.g. an IsInstalling left
        // set by a failed apply, which would permanently disable the Install button).
        ResetBannerState();

        _pendingUpdate = update;
        IsUpdateAvailable = true;
        UpdateVersion = update.Version;
        StatusText = $"Version {update.Version} available";
        Log.Info($"Update v{update.Version} found ({update.Size} bytes). Starting auto-download.");

        await AutoDownloadAsync(update);
        return true;
    }

    [RelayCommand]
    private void InstallUpdate()
    {
        var pending = _pendingUpdate;
        if (pending is null || IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        StatusText = "Installing update...";
        Log.Info($"Applying update v{pending.Version} and restarting...");

        // The service swallows its own exceptions and reports failure via the return value, so this
        // branch — not a catch — is what surfaces a failed apply.
        if (!_updateService.ApplyUpdatesAndRestart(pending))
        {
            IsInstalling = false;
            HasError = true;
            CanRetryDownload = false;
            ErrorMessage = "Install failed. Please restart manually.";
            StatusText = "Install failed";
            Log.Error($"Failed to apply update v{pending.Version}.");
        }
    }

    [RelayCommand]
    private async Task RetryDownloadAsync()
    {
        var pending = _pendingUpdate;
        if (pending is null)
        {
            return;
        }

        HasError = false;
        CanRetryDownload = false;
        ErrorMessage = string.Empty;
        await AutoDownloadAsync(pending);
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        Log.Info($"User dismissed update v{_pendingUpdate?.Version}.");
        _pendingUpdate = null;
        IsUpdateAvailable = false;
        ResetBannerState();
    }

    /// <summary>Clears every transient banner flag. Keeps <see cref="_pendingUpdate"/> untouched.</summary>
    private void ResetBannerState()
    {
        HasError = false;
        CanRetryDownload = false;
        ErrorMessage = string.Empty;
        StatusText = string.Empty;
        IsDownloading = false;
        IsUpdateDownloaded = false;
        IsInstalling = false;
        DownloadProgress = 0;
    }

    /// <summary>
    /// Downloads <paramref name="update"/>. Takes the update as a parameter rather than reading the
    /// field: the banner's Dismiss button is live for the whole download and nulls _pendingUpdate,
    /// so the field cannot be trusted across the await.
    /// </summary>
    private async Task AutoDownloadAsync(UpdateInfo update)
    {
        IsDownloading = true;
        HasError = false;
        CanRetryDownload = false;
        ErrorMessage = string.Empty;
        StatusText = "Downloading update...";
        Log.Info($"Downloading update v{update.Version}...");

        var progress = new Progress<int>(percent =>
        {
            DownloadProgress = percent;
            StatusText = $"Downloading update... {percent}%";
        });

        var success = await _updateService.DownloadUpdateAsync(update, progress);

        // Dismissed (or superseded) while the download was in flight: the state it left behind is
        // already clean, so write nothing — otherwise a dismissed banner resurrects as "ready".
        if (!ReferenceEquals(_pendingUpdate, update))
        {
            Log.Info($"Update v{update.Version} was dismissed during download; discarding the result.");
            return;
        }

        IsDownloading = false;

        if (success)
        {
            IsUpdateDownloaded = true;
            StatusText = "Update ready — restart to apply";
            Log.Info($"Update v{update.Version} downloaded. Ready to install.");
        }
        else
        {
            HasError = true;
            CanRetryDownload = true;
            ErrorMessage = "Download failed. Try again later.";
            StatusText = "Download failed";
            Log.Warn($"Update v{update.Version} download failed.");
        }
    }

    private static bool IsVersionNewer(string availableVersion, string currentVersion)
    {
        if (Version.TryParse(availableVersion, out var available) &&
            Version.TryParse(currentVersion, out var current))
        {
            return available > current;
        }

        // Unparseable versions (e.g. a semver pre-release suffix): trust the feed.
        return true;
    }
}
