namespace RemoteHub.Core.Models;

/// <summary>
/// Root persisted document holding the top-level nodes of the connection tree.
/// </summary>
public sealed class ConnectionDocument
{
    public int Version { get; set; } = 1;

    public List<ConnectionNode> Roots { get; set; } = new();
}
