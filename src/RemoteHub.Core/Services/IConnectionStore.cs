using RemoteHub.Core.Models;

namespace RemoteHub.Core.Services;

/// <summary>
/// Loads and saves the <see cref="ConnectionDocument"/> to a configurable path.
/// </summary>
public interface IConnectionStore
{
    /// <summary>
    /// Loads the document from <paramref name="path"/>. If the file is missing, returns a new
    /// empty document (Version = 1, empty Roots).
    /// </summary>
    Task<ConnectionDocument> LoadAsync(string path);

    /// <summary>
    /// Saves the document to <paramref name="path"/>, creating the parent directory if needed
    /// and writing atomically to avoid corruption.
    /// </summary>
    Task SaveAsync(string path, ConnectionDocument doc);
}
