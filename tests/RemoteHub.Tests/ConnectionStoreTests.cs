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
                FullScreen = true,
                RedirectClipboard = false,
                Audio = AudioRedirectionMode.Remote,
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
        Assert.True(web01.Display.FullScreen);
        Assert.False(web01.Display.RedirectClipboard);
        Assert.Equal(AudioRedirectionMode.Remote, web01.Display.Audio);

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
