using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ThemePreference _theme = ThemePreference.System;

    [ObservableProperty]
    private string? _connectionsFilePath;

    public SettingsViewModel(
        ISettingsService settingsService,
        ThemeManager themeManager,
        IDialogService dialogs)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;
        _dialogs = dialogs;
    }

    /// <summary>Available theme choices for the selector.</summary>
    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();

    /// <summary>Loads current settings into the editable properties.</summary>
    public void Load()
    {
        var settings = _settingsService.Load();
        Theme = settings.Theme;
        ConnectionsFilePath = settings.ConnectionsFilePath;
    }

    /// <summary>Persists edited settings and applies the theme live.</summary>
    public void Save()
    {
        var settings = _settingsService.Load();
        settings.Theme = Theme;
        if (!string.IsNullOrWhiteSpace(ConnectionsFilePath))
        {
            settings.ConnectionsFilePath = ConnectionsFilePath;
        }

        _settingsService.Save(settings);
        _themeManager.Apply(Theme);
    }

    /// <summary>Prompts for a connections file path via the dialog service.</summary>
    [RelayCommand]
    private void Browse()
    {
        var path = _dialogs.PickConnectionsFile(save: true);
        if (path is not null)
        {
            ConnectionsFilePath = path;
        }
    }
}
