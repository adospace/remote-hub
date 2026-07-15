namespace RemoteHub.Core.Services;

/// <summary>
/// Persisted application settings. Serialized to <c>%AppData%\RemoteHub\settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>
    /// Path to the connections document. Never null after <c>ISettingsService.Load()</c>;
    /// the service resolves a default under <c>%AppData%\RemoteHub</c> if unset.
    /// </summary>
    public string? ConnectionsFilePath { get; set; }

    public double WindowWidth { get; set; } = 1200;

    public double WindowHeight { get; set; } = 800;

    public bool WindowMaximized { get; set; } = false;
}
