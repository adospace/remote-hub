using RemoteHub.Core.Services;
using Xunit;

namespace RemoteHub.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir;

    public SettingsServiceTests()
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

    [Fact]
    public void Load_NoFile_ReturnsSaneDefaults()
    {
        var service = new SettingsService(_dir);

        var settings = service.Load();

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(1200, settings.WindowWidth);
        Assert.Equal(800, settings.WindowHeight);
        Assert.False(settings.WindowMaximized);
    }

    [Fact]
    public void Load_NoFile_ConnectionsPathNeverNull()
    {
        var service = new SettingsService(_dir);

        var settings = service.Load();

        Assert.False(string.IsNullOrWhiteSpace(settings.ConnectionsFilePath));
        Assert.Equal(Path.Combine(_dir, "connections.json"), settings.ConnectionsFilePath);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var service = new SettingsService(_dir);
        var saved = new AppSettings
        {
            Theme = ThemePreference.Dark,
            ConnectionsFilePath = Path.Combine(_dir, "custom", "my-connections.json"),
            WindowWidth = 1600,
            WindowHeight = 900,
            WindowMaximized = true,
        };

        service.Save(saved);
        var loaded = new SettingsService(_dir).Load();

        Assert.Equal(ThemePreference.Dark, loaded.Theme);
        Assert.Equal(saved.ConnectionsFilePath, loaded.ConnectionsFilePath);
        Assert.Equal(1600, loaded.WindowWidth);
        Assert.Equal(900, loaded.WindowHeight);
        Assert.True(loaded.WindowMaximized);
    }

    [Fact]
    public void Save_WritesSettingsFileToDirectory()
    {
        var service = new SettingsService(_dir);

        service.Save(new AppSettings());

        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void Load_PreservesExplicitConnectionsPath()
    {
        var service = new SettingsService(_dir);
        var explicitPath = Path.Combine("D:", "data", "connections.json");
        service.Save(new AppSettings { ConnectionsFilePath = explicitPath });

        var loaded = new SettingsService(_dir).Load();

        Assert.Equal(explicitPath, loaded.ConnectionsFilePath);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ this is not valid json ]");

        var settings = new SettingsService(_dir).Load();

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.False(string.IsNullOrWhiteSpace(settings.ConnectionsFilePath));
    }
}
