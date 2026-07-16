using RemoteHub.Diagnostics;
using Velopack;
using Velopack.Sources;
// Velopack has its own UpdateInfo/UpdateOptions. Unqualified, those names bind to this namespace's
// types (a namespace member beats a using-directive), so only Velopack's needs an alias.
using VelopackUpdateInfo = Velopack.UpdateInfo;

namespace RemoteHub.Services;

/// <summary>
/// Velopack-backed updater reading the feed from the GitHub Releases page.
/// Every method no-ops when the manager could not be created (no repo configured, or the app was
/// not installed by the Velopack installer), so debug and xcopy runs behave normally.
/// </summary>
internal sealed class WindowsUpdateService : IUpdateService
{
    private readonly UpdateManager? _updateManager;
    private readonly string _repoUrl;

    public bool IsSupported => _updateManager is not null;

    public string CurrentVersion => _updateManager?.CurrentVersion?.ToString() ?? "0.0.0";

    public event EventHandler<UpdateStatus>? StatusChanged;

    public WindowsUpdateService(UpdateOptions options)
    {
        _repoUrl = options.RepoUrl;

        if (string.IsNullOrWhiteSpace(_repoUrl))
        {
            Log.Info("Update repo URL not configured — updates are disabled.");
            return;
        }

        try
        {
            var source = new GithubSource(_repoUrl, accessToken: null, prerelease: false);
            var manager = new UpdateManager(source);

            // Updates can only be applied to a copy the installer laid down; a plain build has no
            // Velopack context, so leave the manager null and let everything no-op.
            if (!manager.IsInstalled)
            {
                Log.Info("Not running an installed build — updates are disabled.");
                return;
            }

            _updateManager = manager;
            Log.Info($"UpdateManager initialized for {_repoUrl}. Current version: {CurrentVersion}.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to initialize UpdateManager for {_repoUrl}.", ex);
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (_updateManager is null)
        {
            return null;
        }

        try
        {
            StatusChanged?.Invoke(this, UpdateStatus.Checking);
            Log.Info($"Checking for updates at {_repoUrl}. Current version: {CurrentVersion}.");

            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo is null)
            {
                // Velopack signals "nothing newer" with null, not an exception.
                Log.Info($"No updates available; v{CurrentVersion} is up to date.");
                StatusChanged?.Invoke(this, UpdateStatus.NoUpdateAvailable);
                return null;
            }

            var target = updateInfo.TargetFullRelease;
            Log.Info($"Update found: v{target.Version} ({target.Size / 1048576.0:F1} MB). Current: v{CurrentVersion}.");
            StatusChanged?.Invoke(this, UpdateStatus.UpdateAvailable);

            return new UpdateInfo
            {
                Version = target.Version.ToString(),
                ReleaseNotes = target.NotesMarkdown ?? string.Empty,
                Size = target.Size,
                NativeUpdateInfo = updateInfo,
            };
        }
        catch (Exception ex)
        {
            Log.Error($"Update check failed (repo {_repoUrl}, current v{CurrentVersion}).", ex);
            StatusChanged?.Invoke(this, UpdateStatus.Error);
            return null;
        }
    }

    public async Task<bool> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null)
    {
        if (_updateManager is null)
        {
            return false;
        }

        try
        {
            StatusChanged?.Invoke(this, UpdateStatus.Downloading);
            Log.Info($"Downloading update v{updateInfo.Version} ({updateInfo.Size / 1048576.0:F1} MB).");

            var nativeInfo = (VelopackUpdateInfo)updateInfo.NativeUpdateInfo;
            var lastLoggedPercent = 0;

            // Velopack reports progress as Action<int>, not IProgress<int>.
            await _updateManager.DownloadUpdatesAsync(nativeInfo, percent =>
            {
                progress?.Report(percent);
                if (percent >= lastLoggedPercent + 25 || percent == 100)
                {
                    Log.Info($"Download progress: {percent}% for v{updateInfo.Version}.");
                    lastLoggedPercent = percent;
                }
            });

            Log.Info($"Update v{updateInfo.Version} downloaded. Ready to install.");
            StatusChanged?.Invoke(this, UpdateStatus.ReadyToInstall);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Update download failed for v{updateInfo.Version}.", ex);
            StatusChanged?.Invoke(this, UpdateStatus.Error);
            return false;
        }
    }

    public bool ApplyUpdatesAndRestart(UpdateInfo updateInfo)
    {
        if (_updateManager is null)
        {
            Log.Warn("UpdateManager not available — cannot apply update.");
            return false;
        }

        try
        {
            Log.Info($"Applying update v{updateInfo.Version} (from v{CurrentVersion}) and restarting. " +
                     "The process terminates after this entry.");

            var nativeInfo = (VelopackUpdateInfo)updateInfo.NativeUpdateInfo;
            _updateManager.ApplyUpdatesAndRestart(nativeInfo.TargetFullRelease);

            // Unreachable on success — Velopack terminates the process inside the call above.
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to apply update v{updateInfo.Version} (from v{CurrentVersion}).", ex);
            StatusChanged?.Invoke(this, UpdateStatus.Error);
            return false;
        }
    }
}
