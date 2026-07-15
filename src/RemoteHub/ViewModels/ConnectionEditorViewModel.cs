using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
// System.Windows.Forms (globally imported via UseWindowsForms) also defines ColorDepth; pin to the model enum.
using ColorDepth = RemoteHub.Core.Models.ColorDepth;

namespace RemoteHub.ViewModels;

/// <summary>
/// Edits an <see cref="RdpConnection"/>'s fields and display settings, including an optional saved
/// password. The password is only editable when the vault is unlocked; it is encrypted before it
/// touches the model and is never held in plaintext beyond the edit.
/// </summary>
public sealed partial class ConnectionEditorViewModel : ObservableObject
{
    private readonly ICredentialProtector _protector;

    public ConnectionEditorViewModel(ICredentialProtector protector)
    {
        _protector = protector;
    }

    /// <summary>True when a master password is set and unlocked, so a password can be stored.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CannotStorePassword))]
    private bool _canStorePassword;

    /// <summary>Inverse of <see cref="CanStorePassword"/>, for showing the "set a master password" hint.</summary>
    public bool CannotStorePassword => !CanStorePassword;

    /// <summary>True when the connection already has a saved password.</summary>
    [ObservableProperty]
    private bool _hasStoredPassword;

    /// <summary>When set, replace the stored password with this plaintext (pushed by the dialog).</summary>
    public string? NewPassword { get; set; }

    /// <summary>When true, drop any stored password.</summary>
    [ObservableProperty]
    private bool _clearStoredPassword;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 3389;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _domain;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private ScreenSizeMode _screenMode = ScreenSizeMode.FitToWindow;

    [ObservableProperty]
    private int _desktopWidth = 1920;

    [ObservableProperty]
    private int _desktopHeight = 1080;

    [ObservableProperty]
    private ColorDepth _colorDepth = ColorDepth.Bpp32;

    [ObservableProperty]
    private bool _fullScreen;

    [ObservableProperty]
    private bool _redirectClipboard = true;

    [ObservableProperty]
    private AudioRedirectionMode _audio = AudioRedirectionMode.Local;

    /// <summary>Enum choices for the display-settings selectors.</summary>
    public IReadOnlyList<ScreenSizeMode> ScreenModes { get; } = Enum.GetValues<ScreenSizeMode>();

    public IReadOnlyList<ColorDepth> ColorDepths { get; } = Enum.GetValues<ColorDepth>();

    public IReadOnlyList<AudioRedirectionMode> AudioModes { get; } = Enum.GetValues<AudioRedirectionMode>();

    /// <summary>
    /// Loads the editor from an existing connection. Called before showing the dialog.
    /// </summary>
    public void Load(RdpConnection connection)
    {
        Name = connection.Name;
        Host = connection.Host;
        Port = connection.Port;
        Username = connection.Username;
        Domain = connection.Domain;
        Description = connection.Description;

        CanStorePassword = _protector.IsUnlocked;
        HasStoredPassword = !string.IsNullOrEmpty(connection.EncryptedPassword);
        NewPassword = null;
        ClearStoredPassword = false;

        var display = connection.Display;
        ScreenMode = display.ScreenMode;
        DesktopWidth = display.DesktopWidth;
        DesktopHeight = display.DesktopHeight;
        ColorDepth = display.ColorDepth;
        FullScreen = display.FullScreen;
        RedirectClipboard = display.RedirectClipboard;
        Audio = display.Audio;
    }

    /// <summary>
    /// Writes edited values back into the connection model.
    /// </summary>
    public void ApplyTo(RdpConnection connection)
    {
        connection.Name = string.IsNullOrWhiteSpace(Name) ? Host : Name;
        connection.Host = Host;
        connection.Port = Port;
        connection.Username = string.IsNullOrWhiteSpace(Username) ? null : Username;
        connection.Domain = string.IsNullOrWhiteSpace(Domain) ? null : Domain;
        connection.Description = string.IsNullOrWhiteSpace(Description) ? null : Description;

        // Password: clear, replace (encrypting first), or leave the existing token untouched.
        if (ClearStoredPassword)
        {
            connection.EncryptedPassword = null;
        }
        else if (!string.IsNullOrEmpty(NewPassword) && _protector.IsUnlocked)
        {
            connection.EncryptedPassword = _protector.Encrypt(NewPassword);
        }

        connection.Display.ScreenMode = ScreenMode;
        connection.Display.DesktopWidth = DesktopWidth;
        connection.Display.DesktopHeight = DesktopHeight;
        connection.Display.ColorDepth = ColorDepth;
        connection.Display.FullScreen = FullScreen;
        connection.Display.RedirectClipboard = RedirectClipboard;
        connection.Display.Audio = Audio;
    }
}
