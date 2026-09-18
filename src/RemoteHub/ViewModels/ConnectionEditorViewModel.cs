using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
// System.Windows.Forms (globally imported via UseWindowsForms) also defines ColorDepth; pin to the model enum.
using ColorDepth = RemoteHub.Core.Models.ColorDepth;

namespace RemoteHub.ViewModels;

/// <summary>
/// Edits an <see cref="RdpConnection"/>: its identity, the folder it lives in, an optional saved
/// password, and every RDP setting the connection applies at connect time. The password is only
/// editable when the vault is unlocked; it is encrypted before it touches the model and is never held
/// in plaintext beyond the edit.
/// </summary>
public sealed partial class ConnectionEditorViewModel : ObservableObject
{
    private const string FolderSeparator = " › ";

    private readonly ICredentialProtector _protector;

    public ConnectionEditorViewModel(ICredentialProtector protector)
    {
        _protector = protector;
    }

    // --- General -------------------------------------------------------------

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

    /// <summary>Where the connection can live: the top level, then every folder by its full path.</summary>
    public IReadOnlyList<Choice<FolderNode?>> Folders { get; private set; } = [];

    /// <summary>The folder the connection is saved into. Changing it moves the connection.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Folder))]
    private Choice<FolderNode?>? _selectedFolder;

    /// <summary>The chosen folder, or null for the top level.</summary>
    public FolderNode? Folder => SelectedFolder?.Value;

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

    // --- Display -------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomResolution))]
    private ScreenSizeMode _screenMode = ScreenSizeMode.FitToWindow;

    /// <summary>Full screen takes the monitor's resolution, so width/height only apply otherwise.</summary>
    public bool HasCustomResolution => ScreenMode != ScreenSizeMode.FullScreen;

    [ObservableProperty]
    private int _desktopWidth = 1920;

    [ObservableProperty]
    private int _desktopHeight = 1080;

    [ObservableProperty]
    private ColorDepth _colorDepth = ColorDepth.Bpp32;

    [ObservableProperty]
    private bool _displayConnectionBar = true;

    [ObservableProperty]
    private bool _pinConnectionBar = true;

    // --- Local resources -----------------------------------------------------

    [ObservableProperty]
    private AudioRedirectionMode _audio = AudioRedirectionMode.Local;

    [ObservableProperty]
    private bool _recordAudio;

    [ObservableProperty]
    private KeyboardHookMode _keyboardHook = KeyboardHookMode.FullScreenOnly;

    [ObservableProperty]
    private bool _redirectClipboard = true;

    [ObservableProperty]
    private bool _redirectPrinters;

    [ObservableProperty]
    private bool _redirectDrives;

    [ObservableProperty]
    private bool _redirectSmartCards;

    [ObservableProperty]
    private bool _redirectPorts;

    // --- Experience ----------------------------------------------------------

    [ObservableProperty]
    private bool _desktopBackground = true;

    [ObservableProperty]
    private bool _fontSmoothing = true;

    [ObservableProperty]
    private bool _desktopComposition = true;

    [ObservableProperty]
    private bool _showWindowContentsWhileDragging = true;

    [ObservableProperty]
    private bool _menuAnimations = true;

    [ObservableProperty]
    private bool _visualStyles = true;

    [ObservableProperty]
    private bool _persistentBitmapCaching = true;

    [ObservableProperty]
    private bool _autoReconnect = true;

    // --- Advanced ------------------------------------------------------------

    [ObservableProperty]
    private ServerAuthenticationMode _serverAuthentication = ServerAuthenticationMode.Warn;

    [ObservableProperty]
    private bool _networkLevelAuthentication = true;

    [ObservableProperty]
    private bool _adminSession;

    [ObservableProperty]
    private string? _startProgram;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesGateway))]
    private GatewayUsage _gatewayUsage = GatewayUsage.None;

    /// <summary>Gates the gateway fields, which mean nothing while no gateway is used.</summary>
    public bool UsesGateway => GatewayUsage != GatewayUsage.None;

    [ObservableProperty]
    private string? _gatewayHost;

    [ObservableProperty]
    private GatewayLogonMethod _gatewayLogonMethod = GatewayLogonMethod.ChooseLater;

    [ObservableProperty]
    private bool _gatewayShareCredentials = true;

    // --- Picker choices ------------------------------------------------------

    public IReadOnlyList<Choice<ScreenSizeMode>> ScreenModes { get; } =
    [
        new(ScreenSizeMode.FitToWindow, "Scale to fit the window"),
        new(ScreenSizeMode.FixedSize, "Fixed size, unscaled"),
        new(ScreenSizeMode.FullScreen, "Full screen"),
    ];

    public IReadOnlyList<Choice<ColorDepth>> ColorDepths { get; } =
    [
        new(ColorDepth.Bpp32, "Highest quality (32 bit)"),
        new(ColorDepth.Bpp24, "True color (24 bit)"),
        new(ColorDepth.Bpp16, "High color (16 bit)"),
        new(ColorDepth.Bpp15, "High color (15 bit)"),
    ];

    public IReadOnlyList<Choice<AudioRedirectionMode>> AudioModes { get; } =
    [
        new(AudioRedirectionMode.Local, "Play on this computer"),
        new(AudioRedirectionMode.Remote, "Play on the remote computer"),
        new(AudioRedirectionMode.None, "Do not play"),
    ];

    public IReadOnlyList<Choice<KeyboardHookMode>> KeyboardHookModes { get; } =
    [
        new(KeyboardHookMode.FullScreenOnly, "Only when using the full screen"),
        new(KeyboardHookMode.Remote, "On the remote computer"),
        new(KeyboardHookMode.Local, "On this computer"),
    ];

    public IReadOnlyList<Choice<ServerAuthenticationMode>> ServerAuthenticationModes { get; } =
    [
        new(ServerAuthenticationMode.Warn, "Warn me"),
        new(ServerAuthenticationMode.ConnectWithoutWarning, "Connect and don't warn me"),
        new(ServerAuthenticationMode.DoNotConnect, "Do not connect"),
    ];

    public IReadOnlyList<Choice<GatewayUsage>> GatewayUsages { get; } =
    [
        new(GatewayUsage.None, "Don't use an RD Gateway"),
        new(GatewayUsage.Always, "Always use this RD Gateway"),
        new(GatewayUsage.IfDirectConnectionFails, "Use it only if a direct connection fails"),
    ];

    public IReadOnlyList<Choice<GatewayLogonMethod>> GatewayLogonMethods { get; } =
    [
        new(GatewayLogonMethod.ChooseLater, "Allow me to select later"),
        new(GatewayLogonMethod.Password, "Ask for password (NTLM)"),
        new(GatewayLogonMethod.SmartCard, "Smart card"),
    ];

    // --- Load / apply --------------------------------------------------------

    /// <summary>
    /// Loads the editor from <paramref name="connection"/>. <paramref name="folder"/> is where it lives
    /// now — or, for a connection not yet in the document, where it will go unless the user picks
    /// another folder. Called before showing the dialog.
    /// </summary>
    public void Load(RdpConnection connection, ConnectionDocument document, FolderNode? folder)
    {
        Name = connection.Name;
        Host = connection.Host;
        Port = connection.Port;
        Username = connection.Username;
        Domain = connection.Domain;
        Description = connection.Description;

        Folders = BuildFolderChoices(document);
        SelectedFolder = Folders.FirstOrDefault(c => ReferenceEquals(c.Value, folder)) ?? Folders[0];

        CanStorePassword = _protector.IsUnlocked;
        HasStoredPassword = !string.IsNullOrEmpty(connection.EncryptedPassword);
        NewPassword = null;
        ClearStoredPassword = false;

        var display = connection.Display;
        ScreenMode = display.ScreenMode;
        DesktopWidth = display.DesktopWidth;
        DesktopHeight = display.DesktopHeight;
        ColorDepth = display.ColorDepth;
        DisplayConnectionBar = display.DisplayConnectionBar;
        PinConnectionBar = display.PinConnectionBar;
        Audio = display.Audio;
        RecordAudio = display.RecordAudio;
        KeyboardHook = display.KeyboardHook;
        RedirectClipboard = display.RedirectClipboard;
        RedirectPrinters = display.RedirectPrinters;
        RedirectDrives = display.RedirectDrives;
        RedirectSmartCards = display.RedirectSmartCards;
        RedirectPorts = display.RedirectPorts;

        var experience = connection.Experience;
        DesktopBackground = experience.DesktopBackground;
        FontSmoothing = experience.FontSmoothing;
        DesktopComposition = experience.DesktopComposition;
        ShowWindowContentsWhileDragging = experience.ShowWindowContentsWhileDragging;
        MenuAnimations = experience.MenuAnimations;
        VisualStyles = experience.VisualStyles;
        PersistentBitmapCaching = experience.PersistentBitmapCaching;
        AutoReconnect = experience.AutoReconnect;

        var advanced = connection.Advanced;
        ServerAuthentication = advanced.ServerAuthentication;
        NetworkLevelAuthentication = advanced.NetworkLevelAuthentication;
        AdminSession = advanced.AdminSession;
        StartProgram = advanced.StartProgram;
        WorkingDirectory = advanced.WorkingDirectory;

        var gateway = connection.Gateway;
        GatewayUsage = gateway.Usage;
        GatewayHost = gateway.Host;
        GatewayLogonMethod = gateway.LogonMethod;
        GatewayShareCredentials = gateway.ShareCredentials;
    }

    /// <summary>
    /// Writes edited values back into the connection model. Moving it to <see cref="Folder"/> is the
    /// caller's job, since that needs the document.
    /// </summary>
    public void ApplyTo(RdpConnection connection)
    {
        connection.Name = string.IsNullOrWhiteSpace(Name) ? Host : Name;
        connection.Host = Host;
        connection.Port = Port;
        connection.Username = NullIfBlank(Username);
        connection.Domain = NullIfBlank(Domain);
        connection.Description = NullIfBlank(Description);

        // Password: clear, replace (encrypting first), or leave the existing token untouched.
        if (ClearStoredPassword)
        {
            connection.EncryptedPassword = null;
        }
        else if (!string.IsNullOrEmpty(NewPassword) && _protector.IsUnlocked)
        {
            connection.EncryptedPassword = _protector.Encrypt(NewPassword);
        }

        var display = connection.Display;
        display.ScreenMode = ScreenMode;
        display.DesktopWidth = DesktopWidth;
        display.DesktopHeight = DesktopHeight;
        display.ColorDepth = ColorDepth;
        display.DisplayConnectionBar = DisplayConnectionBar;
        display.PinConnectionBar = PinConnectionBar;
        display.Audio = Audio;
        display.RecordAudio = RecordAudio;
        display.KeyboardHook = KeyboardHook;
        display.RedirectClipboard = RedirectClipboard;
        display.RedirectPrinters = RedirectPrinters;
        display.RedirectDrives = RedirectDrives;
        display.RedirectSmartCards = RedirectSmartCards;
        display.RedirectPorts = RedirectPorts;

        var experience = connection.Experience;
        experience.DesktopBackground = DesktopBackground;
        experience.FontSmoothing = FontSmoothing;
        experience.DesktopComposition = DesktopComposition;
        experience.ShowWindowContentsWhileDragging = ShowWindowContentsWhileDragging;
        experience.MenuAnimations = MenuAnimations;
        experience.VisualStyles = VisualStyles;
        experience.PersistentBitmapCaching = PersistentBitmapCaching;
        experience.AutoReconnect = AutoReconnect;

        var advanced = connection.Advanced;
        advanced.ServerAuthentication = ServerAuthentication;
        advanced.NetworkLevelAuthentication = NetworkLevelAuthentication;
        advanced.AdminSession = AdminSession;
        advanced.StartProgram = NullIfBlank(StartProgram);
        advanced.WorkingDirectory = NullIfBlank(WorkingDirectory);

        var gateway = connection.Gateway;
        gateway.Usage = GatewayUsage;
        gateway.Host = NullIfBlank(GatewayHost);
        gateway.LogonMethod = GatewayLogonMethod;
        gateway.ShareCredentials = GatewayShareCredentials;
    }

    /// <summary>
    /// The top level followed by every folder, labelled with its full path and ordered the way the
    /// tree shows them (depth-first, alphabetical), so nested folders sit under their parent.
    /// </summary>
    private static List<Choice<FolderNode?>> BuildFolderChoices(ConnectionDocument document)
    {
        var choices = new List<Choice<FolderNode?>> { new(null, "(Top level)") };

        void Walk(IEnumerable<ConnectionNode> nodes, string prefix)
        {
            foreach (var folder in nodes.OfType<FolderNode>()
                                        .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var path = prefix + folder.Name;
                choices.Add(new Choice<FolderNode?>(folder, path));
                Walk(folder.Children, path + FolderSeparator);
            }
        }

        Walk(document.Roots, string.Empty);
        return choices;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
