using RemoteHub.Core.Models;
using RemoteHub.Core.Services;
using Xunit;

namespace RemoteHub.Tests;

public class ConnectionStoreTests : IDisposable
{
    private readonly string _dir;

    public ConnectionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RemoteHubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static ConnectionDocument BuildNestedDocument()
    {
        var web01 = new RdpConnection
        {
            Name = "Web01",
            Host = "web01.contoso.com",
            Port = 3389,
            Username = "svc_web",
            Domain = "CONTOSO",
            Description = "Primary web front-end",
            Display = new RdpDisplaySettings
            {
                ScreenMode = ScreenSizeMode.FixedSize,
                DesktopWidth = 2560,
                DesktopHeight = 1440,
                ColorDepth = ColorDepth.Bpp24,
                DisplayConnectionBar = false,
                PinConnectionBar = false,
                RedirectClipboard = false,
                Audio = AudioRedirectionMode.Remote,
                RecordAudio = true,
                KeyboardHook = KeyboardHookMode.Remote,
                RedirectPrinters = true,
                RedirectDrives = true,
                RedirectSmartCards = true,
                RedirectPorts = true,
            },
            Experience = new RdpExperienceSettings
            {
                DesktopBackground = false,
                FontSmoothing = false,
                DesktopComposition = false,
                ShowWindowContentsWhileDragging = false,
                MenuAnimations = false,
                VisualStyles = false,
                PersistentBitmapCaching = false,
                AutoReconnect = false,
            },
            Advanced = new RdpAdvancedSettings
            {
                ServerAuthentication = ServerAuthenticationMode.DoNotConnect,
                NetworkLevelAuthentication = false,
                AdminSession = true,
                StartProgram = @"C:\Tools\app.exe",
                WorkingDirectory = @"C:\Tools",
            },
            Gateway = new RdpGatewaySettings
            {
                Usage = GatewayUsage.IfDirectConnectionFails,
                Host = "gw.contoso.com",
                LogonMethod = GatewayLogonMethod.SmartCard,
                ShareCredentials = false,
            },
        };

        var db01 = new RdpConnection { Name = "DB01", Host = "db01.contoso.com", Port = 3391 };

        var databases = new FolderNode { Name = "Databases", IsExpanded = false, Children = { db01 } };
        var production = new FolderNode { Name = "Production", Children = { web01, databases } };
        var jump = new RdpConnection { Name = "Jump", Host = "jump.contoso.com" };

        return new ConnectionDocument { Version = 1, Roots = { production, jump } };
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsStructureAndFields()
    {
        var store = new ConnectionStore();
        var path = Path.Combine(_dir, "connections.json");
        var doc = BuildNestedDocument();

        await store.SaveAsync(path, doc);
        var loaded = await store.LoadAsync(path);

        Assert.Equal(1, loaded.Version);
        Assert.Equal(2, loaded.Roots.Count);

        var production = Assert.IsType<FolderNode>(loaded.Roots[0]);
        Assert.Equal("Production", production.Name);
        Assert.Equal(2, production.Children.Count);

        var web01 = Assert.IsType<RdpConnection>(production.Children[0]);
        Assert.Equal("Web01", web01.Name);
        Assert.Equal("web01.contoso.com", web01.Host);
        Assert.Equal(3389, web01.Port);
        Assert.Equal("svc_web", web01.Username);
        Assert.Equal("CONTOSO", web01.Domain);
        Assert.Equal("Primary web front-end", web01.Description);
        Assert.Equal(ScreenSizeMode.FixedSize, web01.Display.ScreenMode);
        Assert.Equal(2560, web01.Display.DesktopWidth);
        Assert.Equal(1440, web01.Display.DesktopHeight);
        Assert.Equal(ColorDepth.Bpp24, web01.Display.ColorDepth);
        Assert.False(web01.Display.DisplayConnectionBar);
        Assert.False(web01.Display.PinConnectionBar);
        Assert.False(web01.Display.RedirectClipboard);
        Assert.Equal(AudioRedirectionMode.Remote, web01.Display.Audio);
        Assert.True(web01.Display.RecordAudio);
        Assert.Equal(KeyboardHookMode.Remote, web01.Display.KeyboardHook);
        Assert.True(web01.Display.RedirectPrinters);
        Assert.True(web01.Display.RedirectDrives);
        Assert.True(web01.Display.RedirectSmartCards);
        Assert.True(web01.Display.RedirectPorts);

        Assert.False(web01.Experience.DesktopBackground);
        Assert.False(web01.Experience.FontSmoothing);
        Assert.False(web01.Experience.DesktopComposition);
        Assert.False(web01.Experience.ShowWindowContentsWhileDragging);
        Assert.False(web01.Experience.MenuAnimations);
        Assert.False(web01.Experience.VisualStyles);
        Assert.False(web01.Experience.PersistentBitmapCaching);
        Assert.False(web01.Experience.AutoReconnect);

        Assert.Equal(ServerAuthenticationMode.DoNotConnect, web01.Advanced.ServerAuthentication);
        Assert.False(web01.Advanced.NetworkLevelAuthentication);
        Assert.True(web01.Advanced.AdminSession);
        Assert.Equal(@"C:\Tools\app.exe", web01.Advanced.StartProgram);
        Assert.Equal(@"C:\Tools", web01.Advanced.WorkingDirectory);

        Assert.Equal(GatewayUsage.IfDirectConnectionFails, web01.Gateway.Usage);
        Assert.Equal("gw.contoso.com", web01.Gateway.Host);
        Assert.Equal(GatewayLogonMethod.SmartCard, web01.Gateway.LogonMethod);
        Assert.False(web01.Gateway.ShareCredentials);

        var databases = Assert.IsType<FolderNode>(production.Children[1]);
        Assert.Equal("Databases", databases.Name);
        Assert.False(databases.IsExpanded);
        var db01 = Assert.IsType<RdpConnection>(Assert.Single(databases.Children));
        Assert.Equal("DB01", db01.Name);
        Assert.Equal(3391, db01.Port);

        var jump = Assert.IsType<RdpConnection>(loaded.Roots[1]);
        Assert.Equal("Jump", jump.Name);
    }

    [Fact]
    public async Task Load_DocumentSavedBeforeTheNewSettings_GetsTheirDefaults()
    {
        // A connection as older builds wrote it: only the original display keys (including the
        // retired, never-applied "fullScreen"), and no experience/advanced/gateway sections.
        const string json = """
            {
              "version": 2,
              "roots": [
                {
                  "$type": "rdp",
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Legacy",
                  "host": "legacy.contoso.com",
                  "port": 3389,
                  "display": {
                    "screenMode": "fixedSize",
                    "desktopWidth": 1280,
                    "desktopHeight": 720,
                    "colorDepth": "bpp16",
                    "fullScreen": true,
                    "redirectClipboard": false,
                    "audio": "none"
                  }
                }
              ]
            }
            """;
        var path = Path.Combine(_dir, "legacy.json");
        await File.WriteAllTextAsync(path, json);

        var loaded = await new ConnectionStore().LoadAsync(path);

        var legacy = Assert.IsType<RdpConnection>(Assert.Single(loaded.Roots));
        Assert.Equal(ScreenSizeMode.FixedSize, legacy.Display.ScreenMode);
        Assert.Equal(1280, legacy.Display.DesktopWidth);
        Assert.Equal(ColorDepth.Bpp16, legacy.Display.ColorDepth);
        Assert.False(legacy.Display.RedirectClipboard);
        Assert.Equal(AudioRedirectionMode.None, legacy.Display.Audio);

        // New settings fall back to their defaults, which mirror the RDP control's own.
        Assert.True(legacy.Display.DisplayConnectionBar);
        Assert.Equal(KeyboardHookMode.FullScreenOnly, legacy.Display.KeyboardHook);
        Assert.False(legacy.Display.RedirectDrives);
        Assert.True(legacy.Experience.AutoReconnect);
        Assert.Equal(ServerAuthenticationMode.Warn, legacy.Advanced.ServerAuthentication);
        Assert.True(legacy.Advanced.NetworkLevelAuthentication);
        Assert.Equal(GatewayUsage.None, legacy.Gateway.Usage);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsEmptyDocument()
    {
        var store = new ConnectionStore();
        var path = Path.Combine(_dir, "does-not-exist.json");

        var loaded = await store.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Version);
        Assert.Empty(loaded.Roots);
        Assert.Null(loaded.Security);
    }

    [Fact]
    public async Task Save_CreatesMissingParentDirectory()
    {
        var store = new ConnectionStore();
        var path = Path.Combine(_dir, "nested", "sub", "connections.json");

        await store.SaveAsync(path, BuildNestedDocument());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Save_OverwritesExistingFileAtomically()
    {
        var store = new ConnectionStore();
        var path = Path.Combine(_dir, "connections.json");

        await store.SaveAsync(path, new ConnectionDocument { Roots = { new FolderNode { Name = "First" } } });
        await store.SaveAsync(path, new ConnectionDocument { Roots = { new FolderNode { Name = "Second" } } });

        var loaded = await store.LoadAsync(path);
        var folder = Assert.IsType<FolderNode>(Assert.Single(loaded.Roots));
        Assert.Equal("Second", folder.Name);

        // No leftover temp file.
        Assert.False(File.Exists(path + ".tmp"));
    }
}
