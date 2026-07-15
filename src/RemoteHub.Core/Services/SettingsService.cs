using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteHub.Core.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON under <c>%AppData%\RemoteHub</c>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private const string ConnectionsFileName = "connections.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _directory;

    /// <summary>Production constructor: stores settings under <c>%AppData%\RemoteHub</c>.</summary>
    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemoteHub"))
    {
    }

    /// <summary>
    /// Test/override constructor: stores settings under the supplied directory. Used by tests to
    /// keep persistence isolated in a temp folder.
    /// </summary>
    public SettingsService(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    private string SettingsPath => Path.Combine(_directory, SettingsFileName);

    private string DefaultConnectionsPath => Path.Combine(_directory, ConnectionsFileName);

    /// <inheritdoc />
    public AppSettings Load()
    {
        AppSettings settings;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                settings = string.IsNullOrWhiteSpace(json)
                    ? new AppSettings()
                    : JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable settings should never crash the app — fall back to defaults.
            settings = new AppSettings();
        }

        // Guarantee a usable connections path so callers never see null after Load().
        if (string.IsNullOrWhiteSpace(settings.ConnectionsFilePath))
        {
            settings.ConnectionsFilePath = DefaultConnectionsPath;
        }

        return settings;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(SettingsPath, json);
    }
}
