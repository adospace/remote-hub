using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Services;
using RemoteHub.Services;

namespace RemoteHub.ViewModels;

/// <summary>
/// Backs the settings dialog: theme selection and connections file path.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;

    [ObservableProperty]
    private ThemePreference _theme = ThemePreference.System;

    [ObservableProperty]
    private string? _connectionsFilePath;

    public SettingsViewModel(ISettingsService settingsService, ThemeManager themeManager)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;
    }

    /// <summary>Loads current settings into the editable properties.</summary>
    public void Load()
    {
        // TODO(Implement): read AppSettings and populate properties.
        throw new NotImplementedException();
    }

    /// <summary>Persists edited settings and applies the theme live.</summary>
    public void Save()
    {
        // TODO(Implement): persist via ISettingsService and apply theme via ThemeManager.
        throw new NotImplementedException();
    }
}
