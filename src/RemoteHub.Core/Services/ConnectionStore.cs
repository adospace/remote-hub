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

    // TODO(Implement): missing-file returns empty doc; atomic write (temp then move).
    public Task<ConnectionDocument> LoadAsync(string path)
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(string path, ConnectionDocument doc)
    {
        throw new NotImplementedException();
    }
}
