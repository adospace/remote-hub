using RemoteHub.Core.Models;
using RemoteHub.Core.Serialization;

namespace RemoteHub.Core.Services;

/// <summary>
/// Loads/saves a <see cref="ConnectionDocument"/> to a configurable path via
/// <see cref="ConnectionSerializer"/>.
/// </summary>
public sealed class ConnectionStore : IConnectionStore
{
    private readonly ConnectionSerializer _serializer = new();

    /// <inheritdoc />
    public async Task<ConnectionDocument> LoadAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new ConnectionDocument();
        }

        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConnectionDocument();
        }

        return _serializer.Deserialize(json);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, ConnectionDocument doc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(doc);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = _serializer.Serialize(doc);

        // Write atomically: write to a temp file in the same directory, then move over the
        // target. This avoids leaving a half-written (corrupt) connections file if the process
        // dies mid-write.
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

        // File.Move with overwrite is atomic on the same volume.
        File.Move(tempPath, path, overwrite: true);
    }
}
