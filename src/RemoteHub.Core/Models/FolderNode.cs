namespace RemoteHub.Core.Models;

/// <summary>
/// A folder in the connection tree. May contain both sub-folders and connections.
/// </summary>
public sealed class FolderNode : ConnectionNode
{
    public List<ConnectionNode> Children { get; set; } = new();

    public bool IsExpanded { get; set; } = true;
}
