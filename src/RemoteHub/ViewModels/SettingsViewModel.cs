using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
using RemoteHub.Core.Services;
using RemoteHub.Services;

namespace RemoteHub.ViewModels;

/// <summary>
/// Backs the settings dialog: theme selection, connections file path, and master-password
/// management (set / change / remove). Master-password operations act on the persisted connections
/// document (loading, re-encrypting, and saving it) so they take effect immediately; the main
/// window reloads the document after the dialog closes.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IConnectionStore _store;
    private readonly ICredentialProtector _protector;
    private readonly ThemeManager _themeManager;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ThemePreference _theme = ThemePreference.System;

    [ObservableProperty]
    private string? _connectionsFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MasterPasswordButtonText))]
    [NotifyPropertyChangedFor(nameof(MasterPasswordStatus))]
    [NotifyPropertyChangedFor(nameof(CanRemoveMasterPassword))]
    private bool _hasMasterPassword;

    public SettingsViewModel(
        ISettingsService settingsService,
        IConnectionStore store,
        ICredentialProtector protector,
        ThemeManager themeManager,
        IDialogService dialogs)
    {
        _settingsService = settingsService;
        _store = store;
        _protector = protector;
        _themeManager = themeManager;
        _dialogs = dialogs;
    }

    /// <summary>Available theme choices for the selector.</summary>
    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();

    public string MasterPasswordButtonText =>
        HasMasterPassword ? "Change master password…" : "Set master password…";

    public string MasterPasswordStatus =>
        HasMasterPassword
            ? "A master password protects your saved connection passwords."
            : "No master password set. Set one to store connection passwords.";

    public bool CanRemoveMasterPassword => HasMasterPassword;

    /// <summary>Loads current settings into the editable properties.</summary>
    public void Load()
    {
        var settings = _settingsService.Load();
        Theme = settings.Theme;
        ConnectionsFilePath = settings.ConnectionsFilePath;
        HasMasterPassword = _protector.HasVault;
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

    /// <summary>Sets a master password (first time) or changes it (re-encrypting saved passwords).</summary>
    [RelayCommand]
    private async Task SetOrChangeMasterPasswordAsync()
    {
        var path = CurrentConnectionsPath();
        var doc = await _store.LoadAsync(path);

        if (doc.Security is null)
        {
            var created = _dialogs.CreateMasterPassword(
                "Set master password",
                "Create a master password to encrypt saved connection passwords. " +
                "If you forget it, saved passwords cannot be recovered.");
            if (created is null)
            {
                return;
            }

            doc.Security = _protector.CreateVault(created);
            await _store.SaveAsync(path, doc);
            HasMasterPassword = true;
            return;
        }

        // Changing: make sure we hold the current key so existing secrets can be re-encrypted.
        if (!_protector.IsUnlockedFor(doc.Security))
        {
            var current = _dialogs.PromptMasterPassword("Current master password", "Enter your current master password.");
            if (current is null)
            {
                return;
            }

            if (!_protector.TryUnlock(doc.Security, current))
            {
                _dialogs.Confirm("Master password", "That master password is incorrect.");
                return;
            }
        }

        var next = _dialogs.CreateMasterPassword("Change master password", "Enter a new master password.");
        if (next is null)
        {
            return;
        }

        // Decrypt all saved secrets with the CURRENT key before re-keying.
        var secrets = new List<(RdpConnection Connection, string Plain)>();
        foreach (var connection in EnumerateConnections(doc.Roots))
        {
            if (!string.IsNullOrEmpty(connection.EncryptedPassword) &&
                _protector.TryDecrypt(connection.EncryptedPassword, out var plain))
            {
                secrets.Add((connection, plain));
            }
        }

        doc.Security = _protector.CreateVault(next); // swaps the in-memory key to the new one
        foreach (var (connection, plain) in secrets)
        {
            connection.EncryptedPassword = _protector.Encrypt(plain);
        }

        await _store.SaveAsync(path, doc);
        HasMasterPassword = true;
    }

    /// <summary>Removes the master password and deletes all saved connection passwords.</summary>
    [RelayCommand]
    private async Task RemoveMasterPasswordAsync()
    {
        var path = CurrentConnectionsPath();
        var doc = await _store.LoadAsync(path);
        if (doc.Security is null)
        {
            HasMasterPassword = false;
            return;
        }

        if (!_dialogs.Confirm(
                "Remove master password",
                "This removes the master password and permanently deletes all saved connection passwords. Continue?"))
        {
            return;
        }

        foreach (var connection in EnumerateConnections(doc.Roots))
        {
            connection.EncryptedPassword = null;
        }

        doc.Security = null;
        _protector.Lock();
        await _store.SaveAsync(path, doc);
        HasMasterPassword = false;
    }

    private string CurrentConnectionsPath() =>
        _settingsService.Load().ConnectionsFilePath!;

    private static IEnumerable<RdpConnection> EnumerateConnections(IEnumerable<ConnectionNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is RdpConnection connection)
            {
                yield return connection;
            }
            else if (node is FolderNode folder)
            {
                foreach (var child in EnumerateConnections(folder.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
